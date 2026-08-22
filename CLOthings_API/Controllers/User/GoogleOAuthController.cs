using CLOthings_API.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CLOthings_API.Controllers
{
    // 🟢【新增】
    [Route("api/User/google")]
    [ApiController]
    public class GoogleOAuthController : ControllerBase
    {
        // 🟢【新增】
        private readonly CLOthingsContext _context;

        // 🟢【新增】
        public GoogleOAuthController(CLOthingsContext context)
        {
            _context = context;
        }

        // =============================================
        // GET: api/User/google/login
        // =============================================

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

            // =====================================================
            // 🟢【新增】Google 綁定流程
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
                return Ok(new
                {
                    message = "Google 帳號綁定成功",
                    provider = "Google",
                    email
                });
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

        // 綁定 Google
        // GET: api/User/google/bind
        [HttpGet("bind")]
        [Authorize]
        public IActionResult GoogleBind()
        {
            // 🟢 從目前 CLOthings JWT 取得登入會員 UserId
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("無法取得目前登入會員");
            }

            // 🟢 Google 完成後回到同一個 callback
            var redirectUrl = Url.Action(
                nameof(GoogleLoginCallback),
                "GoogleOAuth"
            );

            var properties = new AuthenticationProperties
            {
                RedirectUri = redirectUrl
            };

            // 🟢 記錄這次 OAuth 的用途
            properties.Items["flow"] = "bind";

            // 🟢 記錄「發起綁定的 CLOthings UserId」
            properties.Items["userId"] = userId;

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
        // DELETE: api/User/google
        [HttpDelete]
        [Authorize]
        public async Task<IActionResult> UnbindGoogle()
        {
            // 🟢【新增】從 JWT 取得目前登入會員 UserId
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            if (!int.TryParse(userId, out var parsedUserId))
            {
                return Unauthorized("無法取得目前登入會員");
            }

            // 🟢【新增】尋找這個會員的 Google 綁定
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