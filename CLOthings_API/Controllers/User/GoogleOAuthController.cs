using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CLOthings_API.Controllers
{
    // 🟢【新增】
    [Route("api/User/google")]
    [ApiController]
    public class GoogleOAuthController : ControllerBase
    {
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
    }
}