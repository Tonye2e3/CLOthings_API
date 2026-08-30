using CLOthings_API.Hubs;
using CLOthings_API.Middleware;
using CLOthings_API.Models;
using CLOthings_API.Services;
using CLOthings_API.Services.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Google OAuth 綁定流程的一次性短效票證
builder.Services.AddMemoryCache();

// AddSignalR()：註冊聊天室要用的 WebSocket 即時通訊服務（ChatHub.cs 靠這個才能運作）。
builder.Services.AddSignalR();

// ASP.NET Core Identity 的密碼雜湊服務（PasswordHasher<T>）
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// 呼叫 LINE Pay API 要用（GroupPaymentController 的 LINE Pay 那幾支端點）
builder.Services.AddHttpClient();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Email 發送 service（EmailService.cs）
builder.Services.AddScoped<EmailService>();

// JWT 驗證用的服務 service（AuthTokenService.cs）
builder.Services.AddScoped<AuthTokenService>();
// Email 驗證 service（EmailVerificationService.cs）
builder.Services.AddScoped<EmailVerificationService>();
// 忘記密碼 Email 發送 service（ForgotEmailService.cs）
builder.Services.AddScoped<ForgotEmailService>();

builder.Services.AddDbContext<CLOthingsContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("CLOthingsConnection"));
});

// JWT 驗證
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })

    // Google OAuth 暫存登入狀態
    .AddCookie("External")

    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    builder.Configuration["Jwt:Key"]!
                )
            ),
            // JWT 過期時間不給額外容許誤差
            ClockSkew = TimeSpan.Zero
        };


        // 一般的 API 請求，瀏覽器可以夾帶 Authorization: Bearer xxx 這個標頭，
        // 但 WebSocket 連線（ChatHub 用的就是這個）沒辦法這樣夾帶自訂標頭，
        // 前端 SignalR 用戶端會改成把 Token 放在網址的查詢字串上
        // （例如 /hub/chat?access_token=xxx）。
        // 這裡多加這段，是告訴 JWT 驗證機制：如果請求是打 /hub 開頭的路徑，
        // 且網址上有帶 access_token，就改成讀那個值來驗證身分，
        // 而不是隻認 Authorization 標頭（一般 API 請求不受影響，邏輯不變）。
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    })

// Google OAuth
.AddGoogle("Google", options =>
{
    // Google 登入完成後，暫時把身分放進 External Cookie
    options.SignInScheme = "External";

    options.ClientId =
        builder.Configuration["Authentication:Google:ClientId"]!;

    options.ClientSecret =
        builder.Configuration["Authentication:Google:ClientSecret"]!;

    // Google 驗證完成後回到 ASP.NET Core
    options.CallbackPath = "/signin-google";
    // 保存 Google 回傳的 Token
    options.SaveTokens = true;
});


//CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5173",                                  // 本機開發用前端
                    "https://victorious-moss-045aad400.7.azurestaticapps.net" // 正式部署前端（Azure Static Web Apps）
                  )
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

// 流量限制：目前只套用在團購（Group）模組容易被灌爆的端點
// 結帳／加購物車／建立付款／結算，其餘端點暫不設限
// 用 AddPolicy 依「使用者身分」分桶，而不是全站共用一個額度：
// 這樣「很多不同使用者同時搶購」不會互相卡到彼此的額度，
// 只有「同一個人／同一個來源」短時間狂刷才會被擋下來
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("group", httpContext =>
    {
        var key = httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.User.Identity.Name
                ?? "anonymous"
            : httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,                    // 每個使用者 10 秒內最多 10 次請求
            Window = TimeSpan.FromSeconds(10),
            QueueLimit = 0                        // 超過就直接拒絕，不排隊等待
        });
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});



var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    //.NET10 使用 swaggerUI
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "OpenAPI V1"
        );
    });
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCors("AllowFrontend");

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAuthentication();

app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

app.MapHub<ChatHub>("/hub/chat");


app.Run();