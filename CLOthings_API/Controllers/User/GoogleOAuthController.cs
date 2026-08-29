using CLOthings.Enums;
using CLOthings_API.DTO.User;
using CLOthings_API.Models;
using CLOthings_API.Services.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace CLOthings_API.Controllers
{

    [Route("api/User/google")]
    [ApiController]
    public class GoogleOAuthController : ControllerBase
    {

        private readonly CLOthingsContext _context;
        private readonly IMemoryCache _cache;
        private readonly AuthTokenService _authTokenService;
        private readonly IConfiguration _configuration;


        public GoogleOAuthController(CLOthingsContext context, IMemoryCache cache, AuthTokenService authTokenService, IConfiguration configuration)
        {
            _context = context;
            _cache = cache;
            _authTokenService = authTokenService;
            _configuration = configuration;
        }

        // GET: api/User/google/login
        [HttpGet("login")]
        [AllowAnonymous]
        public IActionResult GoogleLogin()
        {
            // Google 驗證完成後
            // 我們自己的下一站
            var redirectUrl = Url.Action(
                nameof(GoogleLoginCallback),
                "GoogleOAuth"
            );

            var properties =
                new AuthenticationProperties
                {
                    RedirectUri = redirectUrl
                };

            properties.Items["flow"] = "login";

            // 每次 Google 登入都顯示帳號選擇畫面
            properties.Parameters["prompt"] = "select_account";

            // 指定使用 Program.cs 裡面的 "Google"
            return Challenge(
                properties,
                "Google"
            );
        }

        // GET: api/User/google/callback
        [HttpGet("callback")]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleLoginCallback()
        {
            // 從 External Cookie 取得剛剛 Google 驗證完成的身分
            var result = await HttpContext.AuthenticateAsync("External");

            // Google 驗證失敗，或沒有取得身分
            if (!result.Succeeded || result.Principal == null)
            {
                return Unauthorized("Google 驗證失敗");
            }

            // 🟢【新增】取得 OAuth 流程資訊

            var properties = result.Properties;

            string? flow = null;
            string? userId = null;

            if (properties != null)
            {
                properties.Items.TryGetValue("flow", out flow);
                properties.Items.TryGetValue("userId", out userId);
            }
            // Google 的使用者 Claims
            var claims = result.Principal.Claims;

            // Google 使用者唯一 ID
            var providerUserId = claims
                .FirstOrDefault(c =>
                    c.Type == ClaimTypes.NameIdentifier)
                ?.Value;

            // Google Email
            var email = claims
                .FirstOrDefault(c =>
                    c.Type == ClaimTypes.Email)
                ?.Value;

            // Google 顯示名稱
            var name = claims
                .FirstOrDefault(c =>
                    c.Type == ClaimTypes.Name)
                ?.Value;

            // 🟢 Google 登入流程
            if (flow == "login")
            {
                // Google 沒有提供唯一 ID，不能繼續
                if (string.IsNullOrEmpty(providerUserId))
                {
                    return Unauthorized("無法取得 Google 使用者識別碼");
                }

                // 用 Google 的唯一 ID 尋找綁定紀錄
                var googleOAuth = await _context.UserOAuth
                    .Include(o => o.User)
                    .FirstOrDefaultAsync(o =>
                        o.Provider == "Google" &&
                        o.ProviderUserId == providerUserId
                    );

                // 還沒有任何 CLOthings 帳號綁定這個 Google
                if (googleOAuth == null)
                {
                    // ① Google 必須提供 Email
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        return BadRequest(
                            "Google 沒有提供 Email，無法建立會員"
                        );
                    }

                    // =============================================
                    // ② 檢查 Email 是否已存在 CLOthings
                    // =============================================
                    var emailAlreadyExists = await _context.User
                        .AnyAsync(u => u.Email == email);

                    if (emailAlreadyExists)
                    {
                        return Conflict(new
                        {
                            message =
                                "此 Email 已有 CLOthings 帳號，請先使用原帳號登入後綁定 Google"
                        });
                    }

                    // ③ 建立新的 CLOthings User
                    var newUser = new User
                    {
                        Username = name ?? "Google User",

                        // 先暫時產生唯一 Account
                        Account = $"google_{Guid.NewGuid():N}",

                        Email = email,

                        UserType = (int)UserTypeEnum.User,
                        Status = (int)StatusEnum.Active,

                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };

                    _context.User.Add(newUser);

                    await _context.SaveChangesAsync();

                    // ④ 建立 UserOAuth
                    var newOAuth = new UserOAuth
                    {
                        UserId = newUser.UserId,

                        Provider = "Google",
                        ProviderUserId = providerUserId,

                        Email = email,

                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow,

                        AccessToken = null,
                        RefreshToken = null,
                        ExpiresAt = null
                    };

                    _context.UserOAuth.Add(newOAuth);

                    // ⑤ 建立 UserProfile
                    var newProfile = new UserProfile
                    {
                        UserId = newUser.UserId
                    };

                    _context.UserProfile.Add(newProfile);

                    await _context.SaveChangesAsync();

                    // ⑥ 讓後面的登入流程繼續使用 newUser
                    // =============================================
                    googleOAuth = newOAuth;
                    googleOAuth.User = newUser;
                }

                // 找到對應會員
                var user = googleOAuth.User;

                if (user == null)
                {
                    return NotFound("找不到對應會員");
                }

                // 🟢 建立 CLOthings 登入 Token
                var tokenResult =
                    await _authTokenService
                        .CreateLoginTokenAsync(user);

                // 🟢 Refresh Token 放進 HttpOnly Cookie
                Response.Cookies.Append(
                    "refreshToken",
                    tokenResult.RefreshToken,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.None,
                        Expires = tokenResult.RefreshTokenExpiresAt
                    }
                );

                // 🟢建立一次性 Google Login Ticket

                // 產生隨機 Ticket
                var ticket = Guid.NewGuid().ToString("N");

                // 將登入結果暫時存進 MemoryCache
                _cache.Set(
                    $"google-login:{ticket}",
                    tokenResult,
                    TimeSpan.FromMinutes(1)
                );

                // Redirect 回 Vue
                var frontendUrl = _configuration["Frontend:BaseUrl"];

                return Redirect(
                    $"{frontendUrl}/oauth/google?ticket={ticket}"
                );

            }

            // =====================================================
            // 🟢Google 綁定流程
            // =====================================================
            if (flow == "bind")
            {
                // 🟢【新增】檢查 Google 是否有提供唯一 ID
                if (string.IsNullOrEmpty(providerUserId))
                {
                    return BadRequest("無法取得 Google 使用者 ID");
                }

                // 🟢【新增】把 OAuth 帶回來的 userId 轉成 int
                if (!int.TryParse(userId, out var parsedUserId))
                {
                    return BadRequest("無效的 UserId");
                }

                // 🟢【新增】確認 CLOthings 會員真的存在
                var userExists = await _context.User
                    .AnyAsync(u => u.UserId == parsedUserId);

                if (!userExists)
                {
                    return NotFound("找不到使用者");
                }

                // =================================================
                // 🟢【新增】檢查這個 Google 帳號有沒有被別人綁過
                // =================================================
                var googleAlreadyBound = await _context.UserOAuth
                .AnyAsync(o =>
                    o.Provider == "Google" &&
                    o.ProviderUserId == providerUserId
                );

                if (googleAlreadyBound)
                {
                    return Conflict("此 Google 帳號已經綁定其他會員");
                }

                // =================================================
                // 🟢【新增】檢查這個會員自己是否已經綁過 Google
                // =================================================
                var userAlreadyBound = await _context.UserOAuth
                    .AnyAsync(o =>
                        o.UserId == parsedUserId &&
                        o.Provider == "Google"
                    );

                if (userAlreadyBound)
                {
                    return Conflict("此會員已經綁定 Google 帳號");
                }

                // =================================================
                // 🟢【新增】建立 UserOAuth
                // =================================================
                var userOAuth = new UserOAuth
                {
                    UserId = parsedUserId,
                    Provider = "Google",
                    ProviderUserId = providerUserId,
                    Email = email,

                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,

                    AccessToken = null,
                    RefreshToken = null,
                    ExpiresAt = null
                };

                // 🟢【新增】寫入資料庫
                _context.UserOAuth.Add(userOAuth);

                await _context.SaveChangesAsync();

                // 🟢【新增】綁定成功
                var frontendUrl = _configuration["Frontend:BaseUrl"];

                return Redirect($"{frontendUrl}/user");
            }

            // 暫時回傳資料測試
            return Ok(new
            {
                message = "Google OAuth 登入成功",

                flow,
                userId,

                provider = "Google",
                providerUserId,
                email,
                name
            });
        }

        // 用一次性 Ticket 換取 Google 登入資料
        // POST: api/User/google/exchange
        [HttpPost("exchange")]
        [AllowAnonymous]
        public IActionResult ExchangeGoogleLogin(
            [FromBody] GoogleLoginExchangeRequest request)
        {
            // 1. 檢查 Ticket 是否存在
            if (string.IsNullOrWhiteSpace(request.Ticket))
            {
                return BadRequest("缺少 Google Login Ticket");
            }

            // 2. 從 MemoryCache 找登入資料
            if (!_cache.TryGetValue(
                $"google-login:{request.Ticket}",
                out AuthTokenResult? tokenResult))
            {
                return Unauthorized(
                    "Google Login Ticket 無效或已過期"
                );
            }

            // 3. 保險檢查
            if (tokenResult == null)
            {
                return Unauthorized(
                    "Google Login Ticket 無效"
                );
            }

            // 4. 🟢 Ticket 使用一次後立刻刪除
            _cache.Remove(
                $"google-login:{request.Ticket}"
            );

            // 5. 回傳給 Vue，格式跟一般登入一樣
            return Ok(new
            {
                token = tokenResult.AccessToken,
                userId = tokenResult.UserId,
                name = tokenResult.Name,
                account = tokenResult.Account,
                role = tokenResult.Role
            });
        }

        // 🟢建立 Google 綁定流程
        // POST: api/User/google/bind/start
        [HttpPost("bind/start")]
        [Authorize]
        public IActionResult StartGoogleBind()
        {
            // 從 JWT 取得目前登入會員
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(
                    "無法取得目前登入會員"
                );
            }

            // 建立一個短效的一次性票證
            var ticket = Guid.NewGuid().ToString("N");

            // 暫時存進 Cache
            _cache.Set(
                $"google-bind:{ticket}",
                userId,
                TimeSpan.FromMinutes(5)
            );

            // 回傳給 Vue
            var bindUrl = Url.Action(
                nameof(GoogleBind),
                "GoogleOAuth",
                new { ticket },
                Request.Scheme
            );

            return Ok(new
            {
                url = bindUrl
            });
        }

        // 🟡真正開始 Google OAuth
        // GET: api/User/google/bind?ticket=xxx
        [HttpGet("bind")]
        [AllowAnonymous]
        public IActionResult GoogleBind(
            string ticket
        )
        {

            // 使用一次性 ticket 找回剛才的 UserId
            if (!_cache.TryGetValue(
                $"google-bind:{ticket}",
                out string? userId
            ))
            {
                return Unauthorized(
                    "Google 綁定請求已失效"
                );
            }


            // ticket 用過立刻刪除
            _cache.Remove(
                $"google-bind:{ticket}"
            );

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(
                    "無法取得登入會員"
                );
            }

            var redirectUrl = Url.Action(
                nameof(GoogleLoginCallback),
                "GoogleOAuth"
            );

            var properties =
                new AuthenticationProperties
                {
                    RedirectUri = redirectUrl
                };

            // 告訴 callback：
            // 這次不是登入，是綁定
            properties.Items["flow"] = "bind";
            // 保存 CLOthings UserId
            properties.Items["userId"] = userId;

            // 🟢【新增】綁定時也強制選擇 Google 帳號
            properties.Parameters["prompt"] = "select_account";

            return Challenge(
                properties,
                "Google"
            );
        }

        // 取得目前會員的 Google 綁定狀態
        // GET: api/User/google/status
        [HttpGet("status")]
        [Authorize]
        public async Task<IActionResult> GoogleStatus()
        {
            // 🟢【新增】從 JWT 取得目前會員 UserId
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            if (!int.TryParse(userId, out var parsedUserId))
            {
                return Unauthorized("無法取得目前登入會員");
            }

            // 🟢【新增】尋找 Google 綁定資料
            var googleOAuth = await _context.UserOAuth
                .FirstOrDefaultAsync(o =>
                    o.UserId == parsedUserId &&
                    o.Provider == "Google"
                );

            // 🟢【新增】尚未綁定
            if (googleOAuth == null)
            {
                return Ok(new
                {
                    isBound = false
                });
            }

            // 🟢【新增】已綁定
            return Ok(new
            {
                isBound = true,
                provider = googleOAuth.Provider,
                email = googleOAuth.Email
            });
        }

        // 🟢解除 Google 綁定
        // DELETE: api/User/google/unbind
        [HttpDelete("unbind")]
        [Authorize]
        public async Task<IActionResult> GoogleUnbind()
        {
            // 🟢從 JWT 取得目前登入會員 UserId
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            if (!int.TryParse(userId, out var parsedUserId))
            {
                return Unauthorized("無法取得目前登入會員");
            }

            // 🟢尋找這個會員的 Google 綁定
            var googleOAuth = await _context.UserOAuth
                .FirstOrDefaultAsync(o =>
                    o.UserId == parsedUserId &&
                    o.Provider == "Google"
                );

            // 🟢【新增】根本沒有綁定
            if (googleOAuth == null)
            {
                return NotFound("尚未綁定 Google 帳號");
            }

            // 🟢【新增】刪除 UserOAuth 關聯
            _context.UserOAuth.Remove(googleOAuth);

            // 🟢【新增】儲存
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Google 帳號解除綁定成功"
            });
        }
    }

}