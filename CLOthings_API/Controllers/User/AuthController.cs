using CLOthings_API.DTO.User;
using CLOthings_API.Models;
using CLOthings_API.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace CLOthings_API.Controllers
{
    [Route("api/User")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly CLOthingsContext _context;
        private readonly IConfiguration _configuration;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly AuthTokenService _authTokenService;
        private readonly UserEmailService _userEmailService;

        public AuthController(
            CLOthingsContext context,
            IConfiguration configuration,
            IPasswordHasher<User> passwordHasher,
            AuthTokenService authTokenService,
            UserEmailService userEmailService)
        {
            _context = context;
            _configuration = configuration;
            _passwordHasher = passwordHasher;
            _authTokenService = authTokenService;
            _userEmailService = userEmailService;
        }

        // POST: api/User/login
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult> Login(LoginDTO dto)
        {
            // 1. 根據帳號找使用者
            var user = await _context.User
                .FirstOrDefaultAsync(u => u.Account == dto.Account || u.Email == dto.Account);

            //純第三方登入會員可能沒有 Password
            if (user == null || string.IsNullOrEmpty(user.Password))
            {
                return Unauthorized("帳號或密碼錯誤");
            }

            // 2. 驗證密碼
            var passwordResult =
                _passwordHasher.VerifyHashedPassword(
                    user,
                    user.Password,
                    dto.Password
                );

            if (passwordResult == PasswordVerificationResult.Failed)
            {
                return Unauthorized("帳號或密碼錯誤");
            }

            // 🟢 3. 建立登入 Token
            var tokenResult =
                await _authTokenService
                    .CreateLoginTokenAsync(user);

            // 4. Refresh Token 放 HttpOnly Cookie
            Response.Cookies.Append(
                "refreshToken",
                tokenResult.RefreshToken,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,

                    Expires =
                        tokenResult.RefreshTokenExpiresAt
                }
            );

            // 5. Access Token 回 Vue
            return Ok(new
            {
                token = tokenResult.AccessToken,

                userId = tokenResult.UserId,

                name = tokenResult.Name,

                account = tokenResult.Account,

                role = tokenResult.Role
            });
        }


        // 產生 Refresh Token
        private string GenerateRefreshToken()
        {
            // 產生 64 bytes 的密碼學安全隨機資料
            var randomBytes = RandomNumberGenerator.GetBytes(64);

            // 轉成 Base64 字串，方便傳輸與儲存
            return Convert.ToBase64String(randomBytes);
        }

        // 將 Refresh Token 做 SHA256 Hash
        private string HashRefreshToken(string refreshToken)
        {
            // 將字串轉成 byte[]
            var tokenBytes = Encoding.UTF8.GetBytes(refreshToken);

            // SHA256 Hash
            var hashBytes = SHA256.HashData(tokenBytes);

            // 轉成 Base64 字串
            return Convert.ToBase64String(hashBytes);
        }

        // POST: api/Auth/refresh
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<ActionResult> Refresh()
        {
            // 1. 從 HttpOnly Cookie 取得 Refresh Token
            if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken))
            {
                return Unauthorized("找不到 Refresh Token");
            }

            // 2. 把 Cookie 裡的原始 Refresh Token 做 Hash
            var refreshTokenHash = HashRefreshToken(refreshToken);

            // 3. 用 Hash 去資料庫找 Token
            var storedToken = await _context.UserRefreshToken
                .FirstOrDefaultAsync(t => t.TokenHash == refreshTokenHash);

            if (storedToken == null)
            {
                return Unauthorized("Refresh Token 無效");
            }

            // 4. 檢查是否已經被撤銷
            if (storedToken.RevokedAt != null)
            {
                return Unauthorized("Refresh Token 已失效");
            }

            // 5. 檢查是否過期
            if (storedToken.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                return Unauthorized("Refresh Token 已過期");
            }

            // 6. 找到這顆 Refresh Token 所屬的 User
            var user = await _context.User
                .FirstOrDefaultAsync(u => u.UserId == storedToken.UserId);

            if (user == null)
            {
                return Unauthorized("使用者不存在");
            }

            // 7. 產生新的 Access Token
            var newAccessToken = _authTokenService.CreateAccessToken(user);

            // 8. 產生新的 Refresh Token
            var newRefreshToken = GenerateRefreshToken();

            // 9. 新 Refresh Token 做 Hash
            var newRefreshTokenHash = HashRefreshToken(newRefreshToken);

            // 10. 舊 Refresh Token 設為撤銷
            storedToken.RevokedAt = DateTimeOffset.UtcNow;
            storedToken.ReplacedByTokenHash = newRefreshTokenHash;

            // 11. 建立新的 Refresh Token 資料
            var newStoredToken = new UserRefreshToken
            {
                UserId = user.UserId,
                TokenHash = newRefreshTokenHash,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                RevokedAt = null,
                ReplacedByTokenHash = null
            };

            _context.UserRefreshToken.Add(newStoredToken);

            // 12. 儲存資料庫
            await _context.SaveChangesAsync();

            // 13. 用新的 Refresh Token 取代 Cookie
            Response.Cookies.Append(
                "refreshToken",
                newRefreshToken,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Expires = DateTimeOffset.UtcNow.AddDays(7)
                }
            );

            // 14. 回傳新的 Access Token
            return Ok(new
            {
                token = newAccessToken
            });
        }


        // POST api/User/logout
        [HttpPost("logout")]
        [AllowAnonymous]
        public async Task<IActionResult> Logout()
        {
            // 從 HttpOnly Cookie 取得 Refresh Token
            if (Request.Cookies.TryGetValue("refreshToken", out var refreshToken))
            {
                // 將 Refresh Token Hash
                var refreshTokenHash = HashRefreshToken(refreshToken);

                // 找資料庫裡對應的 Refresh Token
                var storedToken = await _context.UserRefreshToken
                    .FirstOrDefaultAsync(t =>
                        t.TokenHash == refreshTokenHash &&
                        t.RevokedAt == null);

                // 如果找得到，就撤銷它
                if (storedToken != null)
                {
                    storedToken.RevokedAt = DateTimeOffset.UtcNow;

                    await _context.SaveChangesAsync();
                }
            }

            // 不管資料庫有沒有找到 Token
            // 都把瀏覽器的 refreshToken Cookie 刪掉
            Response.Cookies.Delete(
                "refreshToken",
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None
                }
            );

            // 登出成功
            return NoContent();
        }

        // 忘記密碼重設
        // ======================================================        
        // 產生密碼重設 Token
        private string GeneratePasswordResetToken()
        {
            // 產生 64 bytes 密碼學安全亂數
            var randomBytes = RandomNumberGenerator.GetBytes(64);

            // 轉成 Base64 字串
            return Convert.ToBase64String(randomBytes);
        }

        // 將密碼重設 Token 做 SHA256 Hash
        private string HashPasswordResetToken(string token)
        {
            var tokenBytes = Encoding.UTF8.GetBytes(token);

            var hashBytes = SHA256.HashData(tokenBytes);

            return Convert.ToBase64String(hashBytes);
        }

        // POST: api/User/forgot-password
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDTO dto)
        {
            // 1. 用 Email 尋找會員
            var user = await _context.User
                .FirstOrDefaultAsync(u => u.Email == dto.Email);

            // 2. Email 不存在也回相同結果
            if (user == null)
            {
                return Ok(new
                {
                    message = "如果此 Email 已註冊，我們將寄送密碼重設信件"
                });
            }

            // 3. 產生原始 Reset Token
            var resetToken = GeneratePasswordResetToken();

            // 4. Token 做 SHA256 Hash
            var resetTokenHash =
                HashPasswordResetToken(resetToken);

            // 5. 建立資料庫紀錄
            var passwordResetToken =
                new UserPasswordResetToken
                {
                    UserId = user.UserId,

                    TokenHash = resetTokenHash,

                    CreatedAt = DateTimeOffset.UtcNow,

                    // 15 分鐘有效
                    ExpiresAt =
                        DateTimeOffset.UtcNow.AddMinutes(15),

                    UsedAt = null
                };

            // 6. 寫入資料庫
            _context.UserPasswordResetToken.Add(
                passwordResetToken
            );

            await _context.SaveChangesAsync();

            // 7. 建立前端密碼重設網址
            // 從設定取得前端網址
            var frontendBaseUrl = _configuration["Frontend:BaseUrl"];

            if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            {
                throw new InvalidOperationException(
                    "Frontend:BaseUrl 尚未設定"
                );
            }
            // 建立密碼重設網址
            var resetLink =
                $"{frontendBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(resetToken)}";

            // 8. 寄送密碼重設 Email
            await _userEmailService.SendPasswordResetEmailAsync(
                user.Email,
                resetLink
            );

            // 9. 不將 Reset Token 回傳給前端
            return Ok(new
            {
                message =
                    "如果此 Email 已註冊，我們會寄送密碼重設信件"
            });
        }

        // POST: api/User/reset-password
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(
            ResetPasswordDTO dto)
        {
            // 1. 將收到的原始 Token 做 Hash
            var tokenHash =
                HashPasswordResetToken(dto.Token);

            // 2. 用 Hash 找資料庫中的 Reset Token
            var storedToken =
                await _context.UserPasswordResetToken
                    .FirstOrDefaultAsync(t =>
                        t.TokenHash == tokenHash);

            // 3. Token 不存在
            if (storedToken == null)
            {
                return BadRequest("密碼重設連結無效");
            }

            // 4. Token 已經使用過
            if (storedToken.UsedAt != null)
            {
                return BadRequest("密碼重設連結已使用");
            }

            // 5. Token 已過期
            if (storedToken.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                return BadRequest("密碼重設連結已過期");
            }

            // 6. 找 Token 所屬會員
            var user = await _context.User
                .FirstOrDefaultAsync(
                    u => u.UserId == storedToken.UserId);

            if (user == null)
            {
                return BadRequest("使用者不存在");
            }

            // 🟢 新增：檢查新密碼是否與目前密碼相同
            if (!string.IsNullOrEmpty(user.Password))
            {
                var passwordResult =
                    _passwordHasher.VerifyHashedPassword(
                        user,
                        user.Password,
                        dto.NewPassword
                    );

                if (passwordResult != PasswordVerificationResult.Failed)
                {
                    return BadRequest("新密碼不可與目前密碼相同");
                }
            }

            // 7. 新密碼 Hash
            user.Password =
                _passwordHasher.HashPassword(
                    user,
                    dto.NewPassword);

            user.UpdatedAt = DateTimeOffset.UtcNow;

            // 8. 將 Reset Token 標記為已使用
            storedToken.UsedAt = DateTimeOffset.UtcNow;

            // 9. 撤銷這個會員目前所有有效的 Refresh Token
            var activeRefreshTokens = await _context.UserRefreshToken
                .Where(t =>
                    t.UserId == user.UserId &&
                    t.RevokedAt == null)
                .ToListAsync();

            foreach (var refreshToken in activeRefreshTokens)
            {
                refreshToken.RevokedAt = DateTimeOffset.UtcNow;
            }

            // 10. 儲存所有變更
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "密碼重設成功"
            });
        }
    }
}