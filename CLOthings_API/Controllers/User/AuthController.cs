using CLOthings.Enums;
using CLOthings_API.DTO.User;
using CLOthings_API.DTOs;
using CLOthings_API.Models;
using CLOthings_API.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
        private readonly ForgotEmailService _ForgetEmailService;
        private readonly EmailVerificationService _emailVerificationService;

        public AuthController(
            CLOthingsContext context,
            IConfiguration configuration,
            IPasswordHasher<User> passwordHasher,
            AuthTokenService authTokenService,
            ForgotEmailService userEmailService,
            EmailVerificationService emailVerificationService)
        {
            _context = context;
            _configuration = configuration;
            _passwordHasher = passwordHasher;
            _authTokenService = authTokenService;
            _ForgetEmailService = userEmailService;
            _emailVerificationService = emailVerificationService;
        }

        // POST: api/User 註冊
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult> RegsiterUser(RegisterDTO dto)
        {
            // 先檢查帳號是否已存在
            var accountExists = await _context.User
                .AnyAsync(u => u.Account == dto.Account);

            if (accountExists)
            {
                return Conflict("帳號已存在");
            }

            // 檢查 Email 是否已存在
            var emailExists = await _context.User
                .AnyAsync(u => u.Email == dto.Email);

            if (emailExists)
            {
                return Conflict("Email 已存在");
            }

            // 建立新的 User
            var user = new User
            {
                Username = dto.Username,
                Account = dto.Account,
                Email = dto.Email,
                Phone = dto.Phone,

                UserType = (int)UserTypeEnum.User,
                Status = (int)StatusEnum.Active,

                // 🟢 新增：一般帳密註冊，Email 預設尚未驗證
                EmailVerified = false,

                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            // 密碼 Hash
            user.Password = _passwordHasher.HashPassword(
                user,
                dto.Password
            );

            // 先建立 User
            _context.User.Add(user);
            await _context.SaveChangesAsync();

            // 再建立空的 UserProfile
            var profile = new UserProfile
            {
                UserId = user.UserId,

                FirstName = null,
                LastName = null,
                Avatar = null,
                Gender = null,
                Birthday = null,
                StyleTag = null,
                Intro = null
            };

            _context.UserProfile.Add(profile);

            // 🟢 新增：產生 Email 驗證 Token
            var verificationToken = _authTokenService.GenerateSecureToken();

            // 🟢 新增：資料庫只存 Hash
            var verificationTokenHash = _authTokenService.HashToken(verificationToken);

            // 🟢 新增：建立 Email 驗證 Token 紀錄
            var emailVerificationToken =
                new UserEmailVerificationToken
                {
                    UserId = user.UserId,
                    TokenHash = verificationTokenHash,
                    CreatedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
                    UsedAt = null
                };

            _context.UserEmailVerificationToken
                .Add(emailVerificationToken);

            // Profile + Token 一起儲存
            await _context.SaveChangesAsync();

            // 🟢 新增：取得前端網址
            var frontendBaseUrl =
                _configuration["Frontend:BaseUrl"];

            if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            {
                throw new InvalidOperationException(
                    "Frontend:BaseUrl 尚未設定"
                );
            }

            // 🟢 新增：建立 Email 驗證網址
            var verificationLink =
                $"{frontendBaseUrl.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(verificationToken)}";

            // 🟢 新增：寄出驗證信
            await _emailVerificationService
                .SendVerificationEmailAsync(
                    user.Email,
                    verificationLink
                );

            return StatusCode(
                StatusCodes.Status201Created,
                new
                {
                    userId = user.UserId,
                    username = user.Username,
                    account = user.Account,
                    email = user.Email,
                    phone = user.Phone,
                    emailVerified = user.EmailVerified,
                    message = "註冊成功，驗證信已寄出"
                }
            );
        }

        // POST: api/User/verify-email
        // Email 驗證
        [HttpPost("verify-email")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmail(
            VerifyEmailDTO dto)
        {
            // 1. 將前端傳來的原始 Token 做 Hash
            var tokenHash =
                _authTokenService.HashToken(dto.Token);

            // 2. 找資料庫中的 Email 驗證 Token
            var verificationToken =
                await _context.UserEmailVerificationToken
                    .Include(t => t.User)
                    .FirstOrDefaultAsync(t =>
                        t.TokenHash == tokenHash
                    );

            // 3. 找不到 Token
            if (verificationToken == null)
            {
                return BadRequest("驗證連結無效");
            }

            // 4. Token 已經使用過
            if (verificationToken.UsedAt != null)
            {
                return BadRequest("此驗證連結已使用");
            }

            // 5. Token 已過期
            if (verificationToken.ExpiresAt <=
                DateTimeOffset.UtcNow)
            {
                return BadRequest("驗證連結已過期");
            }

            // 6. 找不到對應會員
            var user = verificationToken.User;

            if (user == null)
            {
                return BadRequest("找不到會員資料");
            }

            // 7. Email 驗證成功
            user.EmailVerified = true;

            // Token 標記為已使用
            verificationToken.UsedAt =
                DateTimeOffset.UtcNow;

            user.UpdatedAt =
                DateTimeOffset.UtcNow;

            // 8. 儲存
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Email 驗證成功",
                emailVerified = true
            });
        }

        // POST: api/User/resend-verification-email
        // 重新寄送 Email 驗證信
        [HttpPost("resend-verification-email")]
        public async Task<IActionResult> ResendVerificationEmail()
        {
            // 1. 從 JWT 取得目前登入者 UserId
            var userIdValue =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                );

            if (!int.TryParse(userIdValue, out var userId))
            {
                return Unauthorized();
            }

            // 2. 找目前登入會員
            var user = await _context.User
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId
                );

            if (user == null)
            {
                return NotFound();
            }

            // 3. 已經驗證過就不用再寄
            if (user.EmailVerified)
            {
                return BadRequest(
                    "此 Email 已完成驗證"
                );
            }

            // 4. 讓之前尚未使用的 Token 全部失效
            var oldTokens =
                await _context.UserEmailVerificationToken
                    .Where(t =>
                        t.UserId == user.UserId &&
                        t.UsedAt == null
                    )
                    .ToListAsync();

            var now = DateTimeOffset.UtcNow;

            foreach (var oldToken in oldTokens)
            {
                oldToken.UsedAt = now;
            }

            // 5. 產生新的驗證 Token
            var verificationToken =
                _authTokenService.GenerateSecureToken();

            var verificationTokenHash =
                _authTokenService.HashToken(
                    verificationToken
                );

            // 6. 建立新的 Token 紀錄
            var newToken =
                new UserEmailVerificationToken
                {
                    UserId = user.UserId,

                    TokenHash =
                        verificationTokenHash,

                    CreatedAt = now,

                    ExpiresAt =
                        now.AddMinutes(30),

                    UsedAt = null
                };

            _context.UserEmailVerificationToken
                .Add(newToken);

            await _context.SaveChangesAsync();

            // 7. 建立驗證網址
            var frontendBaseUrl =
                _configuration["Frontend:BaseUrl"];

            if (string.IsNullOrWhiteSpace(
                frontendBaseUrl))
            {
                throw new InvalidOperationException(
                    "Frontend:BaseUrl 尚未設定"
                );
            }

            var verificationLink =
                $"{frontendBaseUrl.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(verificationToken)}";

            // 8. 寄出 Email
            await _emailVerificationService
                .SendVerificationEmailAsync(
                    user.Email,
                    verificationLink
                );

            return Ok(new
            {
                message = "驗證信已重新寄出"
            });
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

        // POST: api/User/refresh
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<ActionResult> Refresh()
        {
            // 1. 從 HttpOnly Cookie 取得 Refresh Token
            if (!Request.Cookies.TryGetValue(
                "refreshToken",
                out var refreshToken))
            {
                return Unauthorized(
                    "找不到 Refresh Token"
                );
            }

            // 2. 交給 AuthTokenService 驗證並 Rotation
            var tokenResult =
                await _authTokenService
                    .RefreshTokenAsync(refreshToken);

            if (tokenResult == null)
            {
                return Unauthorized(
                    "Refresh Token 無效或已過期"
                );
            }

            // 3. 新 Refresh Token 寫回 HttpOnly Cookie
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

            // 4. 新 Access Token 回前端
            return Ok(new
            {
                token = tokenResult.AccessToken
            });
        }


        // POST: api/User/logout
        [HttpPost("logout")]
        [AllowAnonymous]
        public async Task<IActionResult> Logout()
        {
            // 1. 如果 Cookie 裡有 Refresh Token
            if (Request.Cookies.TryGetValue(
                "refreshToken",
                out var refreshToken))
            {
                // 2. 撤銷資料庫中的 Refresh Token
                await _authTokenService
                    .RevokeRefreshTokenAsync(
                        refreshToken
                    );
            }

            // 3. 刪除瀏覽器的 Refresh Token Cookie
            Response.Cookies.Delete(
                "refreshToken",
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None
                }
            );

            return NoContent();
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
            var resetToken = _authTokenService.GenerateSecureToken();

            // 4. Token 做 SHA256 Hash
            var resetTokenHash =
                _authTokenService.HashToken(resetToken);

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
            await _ForgetEmailService.SendPasswordResetEmailAsync(
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
                _authTokenService.HashToken(dto.Token);

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