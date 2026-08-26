using CLOthings.Enums;
using CLOthings_API.DTO.User;
using CLOthings_API.Models;
using CLOthings_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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

        public AuthController(
            CLOthingsContext context,
            IConfiguration configuration,
            IPasswordHasher<User> passwordHasher,
            AuthTokenService authTokenService)
        {
            _context = context;
            _configuration = configuration;
            _passwordHasher = passwordHasher;
            _authTokenService = authTokenService;
        }

        // POST: api/User/login
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult> Login(LoginDTO dto)
        {
            // 1. 根據帳號找使用者
            var user = await _context.User
                .FirstOrDefaultAsync(u => u.Account == dto.Account);

            if (user == null)
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

        // 產生 JWT Access Token
        private string GenerateAccessToken(User user)
        {


            var role = ((UserTypeEnum)user.UserType).ToString();

            var claims = new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.UserId.ToString()
                ),

                new Claim(
                    ClaimTypes.Name,
                    user.Username
                ),

                new Claim(
                    "account",
                    user.Account
                ),

                new Claim(
                    ClaimTypes.Role,
                    role
                )
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _configuration["Jwt:Key"]!
                )
            );

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(20),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler()
                .WriteToken(token);

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
            var newAccessToken = GenerateAccessToken(user);

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
    }
}