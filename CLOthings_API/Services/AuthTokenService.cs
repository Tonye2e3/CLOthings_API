using CLOthings.Enums;
using CLOthings_API.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CLOthings_API.Services
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

            // 4. 儲存 Refresh Token
            var userRefreshToken =
                new UserRefreshToken
                {
                    UserId = user.UserId,

                    TokenHash = refreshTokenHash,

                    CreatedAt =
                        DateTimeOffset.UtcNow,

                    ExpiresAt =
                        DateTimeOffset.UtcNow.AddDays(7),

                    RevokedAt = null,

                    ReplacedByTokenHash = null
                };

            _context.UserRefreshToken.Add(
                userRefreshToken
            );

            await _context.SaveChangesAsync();

            // 5. 回傳登入需要的資料
            return new AuthTokenResult
            {
                AccessToken = accessToken,

                RefreshToken = refreshToken,

                RefreshTokenExpiresAt =
                    DateTimeOffset.UtcNow.AddDays(7),

                UserId = user.UserId,

                Name = user.Username,

                Account = user.Account,

                Role =
                    ((UserTypeEnum)user.UserType)
                    .ToString()
            };
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