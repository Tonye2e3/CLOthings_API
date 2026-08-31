using CLOthings.Enums;
using CLOthings_API.DTOs.User;
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
            var refreshToken = GenerateSecureToken();

            // 3. Refresh Token Hash
            var refreshTokenHash = HashToken(refreshToken);

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

        // refreshToken 流程
        public async Task<AuthTokenResult?> RefreshTokenAsync(string refreshToken)
        {
            // ① Cookie 裡的 Refresh Token → SHA256
            var oldTokenHash = HashToken(refreshToken);

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
                GenerateSecureToken();

            var newRefreshTokenHash =
                HashToken(newRefreshToken);
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

        // =========================================
        // 撤銷 Refresh Token
        // =========================================
        public async Task RevokeRefreshTokenAsync(
            string refreshToken)
        {
            // 原始 Token → SHA-256
            var tokenHash =
                HashToken(refreshToken);

            // 找尚未被撤銷的 Token
            var storedToken =
                await _context.UserRefreshToken
                    .FirstOrDefaultAsync(t =>
                        t.TokenHash == tokenHash &&
                        t.RevokedAt == null
                    );

            // 找不到代表已失效或不存在
            if (storedToken == null)
            {
                return;
            }

            // 撤銷 Token
            storedToken.RevokedAt =
                DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync();
        }

        // 提供 Refresh 流程建立新的 Access Token
        public string CreateAccessToken(User user)
        {
            return GenerateAccessToken(user);
        }

        // =========================================
        // 2FA 暫時登入 Token
        // 帳號密碼驗證成功，但尚未完成 TOTP 時使用
        // 有效時間：5 分鐘
        // =========================================
        public string GenerateTwoFactorToken(User user)
        {
            var claims = new[]
            {
        // 記錄這個 Token 屬於哪個會員
        new Claim(
            ClaimTypes.NameIdentifier,
            user.UserId.ToString()
                    ),

                    // 標記這不是正式 Access Token
                    new Claim(
                        "token_type",
                        "2fa_pending"
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

                    // 2FA Token 只允許使用 5 分鐘
                    expires:
                        DateTime.UtcNow.AddMinutes(5),

                    signingCredentials:
                        credentials
                );

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }

        // =========================================
        // 驗證 2FA 暫時登入 Token
        // 成功：回傳 UserId
        // 失敗：回傳 null
        // =========================================
        public int? ValidateTwoFactorToken(string twoFactorToken)
        {
            try
            {
                var key =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            _configuration["Jwt:Key"]!
                        )
                    );

                var validationParameters =
                    new TokenValidationParameters
                    {
                        // 驗證簽章
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = key,

                        // 驗證 Issuer
                        ValidateIssuer = true,
                        ValidIssuer =
                            _configuration["Jwt:Issuer"],

                        // 驗證 Audience
                        ValidateAudience = true,
                        ValidAudience =
                            _configuration["Jwt:Audience"],

                        // 驗證 5 分鐘有效期限
                        ValidateLifetime = true,

                        // 不額外寬限時間
                        ClockSkew = TimeSpan.Zero
                    };

                var tokenHandler =
                    new JwtSecurityTokenHandler();

                var principal =
                    tokenHandler.ValidateToken(
                        twoFactorToken,
                        validationParameters,
                        out _
                    );

                // 確認這顆 Token 真的是 2FA Pending Token
                var tokenType =
                    principal.FindFirst("token_type")?.Value;

                if (tokenType != "2fa_pending")
                {
                    return null;
                }

                // 取得 Token 裡的 UserId
                var userIdValue =
                    principal.FindFirst(
                        ClaimTypes.NameIdentifier
                    )?.Value;

                if (!int.TryParse(
                    userIdValue,
                    out var userId))
                {
                    return null;
                }

                return userId;
            }
            catch
            {
                // Token 過期、被修改、簽章錯誤等
                // 全部視為無效
                return null;
            }
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
        // 安全 Token
        // Refresh Token / Email 驗證 / 忘記密碼共用
        // =========================================

        // 產生密碼學安全 Token
        public string GenerateSecureToken()
        {
            var randomBytes =
                RandomNumberGenerator.GetBytes(64);

            return Convert.ToBase64String(
                randomBytes
            );
        }

        // 將 Token 做 SHA-256 Hash
        public string HashToken(string token)
        {
            var tokenBytes =
                Encoding.UTF8.GetBytes(token);

            var hashBytes =
                SHA256.HashData(tokenBytes);

            return Convert.ToBase64String(
                hashBytes
            );
        }
    }
}