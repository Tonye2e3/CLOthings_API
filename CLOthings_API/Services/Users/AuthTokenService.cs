using CLOthings.Enums;
using CLOthings_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CLOthings_API.Services.Users
{
    public class AuthTokenService
    {
        private readonly CLOthingsContext _context;
        private readonly IConfiguration _configuration;

        public AuthTokenService(
            CLOthingsContext context,
            IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // =========================================
        // 建立完整登入 Token
        // =========================================
        public async Task<AuthTokenResult> CreateLoginTokenAsync(User user)
        {
            // 1. Access Token
            var accessToken = GenerateAccessToken(user);

            // 2. Refresh Token
            var refreshToken = GenerateRefreshToken();

            // 3. Refresh Token Hash
            var refreshTokenHash =
                HashRefreshToken(refreshToken);

            // 這裡使用 DateTimeOffset.UtcNow 來取得當前的 UTC 時間，並將 Refresh Token 的過期時間設置為 7 天後
            var now = DateTimeOffset.UtcNow;
            // Refresh Token 過期時間只計算一次
            var refreshTokenExpiresAt = now.AddDays(7);

            // 4. 儲存 Refresh Token 到資料庫
            var userRefreshToken =
                new UserRefreshToken
                {
                    UserId = user.UserId,

                    TokenHash = refreshTokenHash,

                    CreatedAt = now,
                    // Refresh Token 過期時間
                    ExpiresAt = refreshTokenExpiresAt,

                    RevokedAt = null,

                    ReplacedByTokenHash = null
                };

            _context.UserRefreshToken.Add(
                userRefreshToken
            );

            await _context.SaveChangesAsync();

            // 5. 回傳登入需要的資料 給前端
            return new AuthTokenResult
            {
                AccessToken = accessToken,

                RefreshToken = refreshToken,
                // Refresh Token 過期時間
                RefreshTokenExpiresAt = refreshTokenExpiresAt,

                UserId = user.UserId,

                Name = user.Username,

                Account = user.Account,

                Role =
                    ((UserTypeEnum)user.UserType)
                    .ToString()
            };
        }

        public async Task<AuthTokenResult?> RefreshTokenAsync(string refreshToken)
        {
            // ① Cookie 裡的 Refresh Token → SHA256
            var oldTokenHash = HashRefreshToken(refreshToken);

            // ② 找資料庫紀錄
            var storedToken = await _context.UserRefreshToken
                .Include(r => r.User)
                .FirstOrDefaultAsync(r =>
                    r.TokenHash == oldTokenHash);

            if (storedToken == null)
            {
                return null;
            }

            // ③ 已經被撤銷
            if (storedToken.RevokedAt != null)
            {
                return null;
            }

            // ④ 已經過期
            if (storedToken.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                return null;
            }

            var user = storedToken.User;

            if (user == null)
            {
                return null;
            }

            // ⑤ 產生新的 Access Token
            var newAccessToken =
                GenerateAccessToken(user);

            // ⑥ 產生新的 Refresh Token
            var newRefreshToken =
                GenerateRefreshToken();

            var newRefreshTokenHash =
                HashRefreshToken(newRefreshToken);
            //  新 Refresh Token 過期時間
            var newExpiresAt =
                DateTimeOffset.UtcNow.AddDays(7);

            // ⑦ 舊 Refresh Token 作廢
            storedToken.RevokedAt =
                DateTimeOffset.UtcNow;

            storedToken.ReplacedByTokenHash =
                newRefreshTokenHash;

            // ⑧ 新 Refresh Token 存資料庫
            var newStoredToken = new UserRefreshToken
            {
                UserId = user.UserId,

                TokenHash = newRefreshTokenHash,

                CreatedAt = DateTimeOffset.UtcNow,

                ExpiresAt = newExpiresAt,

                RevokedAt = null,

                ReplacedByTokenHash = null
            };

            _context.UserRefreshToken.Add(newStoredToken);

            await _context.SaveChangesAsync();

            // ⑨ 回傳新的 Token
            return new AuthTokenResult
            {
                AccessToken = newAccessToken,

                RefreshToken = newRefreshToken,

                RefreshTokenExpiresAt = newExpiresAt,

                UserId = user.UserId,

                Name = user.Username,

                Account = user.Account,

                Role =
                    ((UserTypeEnum)user.UserType)
                    .ToString()
            };
        }

        // 🟢【新增】提供 Refresh 流程建立新的 Access Token
        public string CreateAccessToken(User user)
        {
            return GenerateAccessToken(user);
        }

        // =========================================
        // JWT Access Token
        // =========================================
        private string GenerateAccessToken(User user)
        {
            var role =
                ((UserTypeEnum)user.UserType)
                .ToString();

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

            var key =
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(
                        _configuration["Jwt:Key"]!
                    )
                );

            var credentials =
                new SigningCredentials(
                    key,
                    SecurityAlgorithms.HmacSha256
                );

            var token =
                new JwtSecurityToken(
                    issuer:
                        _configuration["Jwt:Issuer"],

                    audience:
                        _configuration["Jwt:Audience"],

                    claims: claims,
                    //Access Token 有效時間
                    expires:
                        DateTime.UtcNow.AddMinutes(20),

                    signingCredentials:
                        credentials
                );

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }


        // =========================================
        // Refresh Token
        // =========================================
        private string GenerateRefreshToken()
        {
            var randomBytes =
                RandomNumberGenerator.GetBytes(64);

            return Convert.ToBase64String(
                randomBytes
            );
        }


        // =========================================
        // Refresh Token Hash
        // =========================================
        private string HashRefreshToken(
            string refreshToken)
        {
            var tokenBytes =
                Encoding.UTF8.GetBytes(
                    refreshToken
                );

            var hashBytes =
                SHA256.HashData(tokenBytes);

            return Convert.ToBase64String(
                hashBytes
            );
        }


    }


    // =============================================
    // Token Service 回傳資料
    // =============================================
    public class AuthTokenResult
    {
        public string AccessToken { get; set; } = null!;

        public string RefreshToken { get; set; } = null!;

        public DateTimeOffset RefreshTokenExpiresAt
        {
            get;
            set;
        }

        public int UserId { get; set; }

        public string Name { get; set; } = null!;

        public string Account { get; set; } = null!;

        public string Role { get; set; } = null!;
    }
}