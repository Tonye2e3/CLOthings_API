using CLOthings_API.DTO.User;
using CLOthings_API.DTOs;
using CLOthings_API.Models;
using CLOthings_API.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;


[Route("api/[controller]")]
[ApiController]
[Authorize]
public class UserController : ControllerBase
{
    private readonly CLOthingsContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly EmailVerificationService _emailVerificationService;
    private readonly AuthTokenService _authTokenService;
    private readonly IConfiguration _configuration;

    public UserController(
        CLOthingsContext context,
        IPasswordHasher<User> passwordHasher,
        EmailVerificationService emailVerificationService,
        AuthTokenService authTokenService,
        IConfiguration configuration)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _emailVerificationService = emailVerificationService;
        _authTokenService = authTokenService;
        _configuration = configuration;
    }

    // GET: api/User
    [HttpGet]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<IEnumerable<UserDTO>>> GetUser()
    {
        var users = await _context.User.Select(e => new UserDTO
        {
            UserId = e.UserId,
            Username = e.Username,
            Account = e.Account,
            Email = e.Email,
            Phone = e.Phone,
        }).ToListAsync();
        return Ok(users);
    }

    // GET: api/User/5
    [Authorize(Roles = "SuperAdmin")]
    [HttpGet("{userid}")]
    public async Task<ActionResult<UserDTO>> GetUser(int userid)
    {
        var user = await _context.User
            .Where(u => u.UserId == userid)
            .Select(u => new UserDTO
            {
                UserId = u.UserId,
                Username = u.Username,
                Account = u.Account,
                Email = u.Email,
                Phone = u.Phone
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    // PUT: api/User/5
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [Authorize(Roles = "SuperAdmin")]
    [HttpPut("{userid}")]
    public async Task<IActionResult> PutUser(
    int userid,
    UpdateUserDTO dto)
    {
        var user = await _context.User.FindAsync(userid);

        if (user == null)
        {
            return NotFound();
        }

        user.Username = dto.Username;
        user.Email = dto.Email;
        user.Phone = dto.Phone;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }


    // DELETE: api/User/5
    [HttpDelete("{userid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> DeleteUser(int? userid)
    {
        var user = await _context.User.FindAsync(userid);
        if (user == null)
        {
            return NotFound();
        }

        _context.User.Remove(user);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private bool UserExists(int? userid)
    {
        return _context.User.Any(e => e.UserId == userid);
    }

    // GET: api/User/me
    [HttpGet("me")]
    public async Task<ActionResult> GetMe()
    {
        // 從 JWT 取得目前登入者的 UserId
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // JWT 裡沒有 UserId 或格式錯誤
        if (!int.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        // 用 UserId 查資料庫
        var user = await _context.User
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
        {
            return NotFound();
        }

        // 只回傳前端需要的資料
        return Ok(new
        {
            userId = user.UserId,
            username = user.Username,
            account = user.Account,
            email = user.Email,
            emailVerified = user.EmailVerified,
            phone = user.Phone,
            countryCode = user.CountryCode,
            twoFactorEnabled = user.TwoFactorEnabled
        });
    }

    // PUT: api/User/me 會員修改資料
    [HttpPut("me")]
    public async Task<IActionResult> PutMe(UpdateUserDTO dto)
    {
        // 1. 從 JWT 取得目前登入者的 UserId
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        // 2. 從資料庫找到目前登入者
        var user = await _context.User
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
        {
            return NotFound();
        }

        // 🟢 記錄 Email 是否有變更
        var emailChanged = !string.Equals(
            user.Email,
            dto.Email,
            StringComparison.OrdinalIgnoreCase);

        // Email 有修改時，先確認前端網址設定存在
        string? frontendBaseUrl = null;

        if (emailChanged)
        {
            frontendBaseUrl = _configuration["Frontend:BaseUrl"];

            if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            {
                return StatusCode(500, "Frontend BaseUrl 尚未設定");
            }
        }

        // 🟢 Email 有變更時，先確認沒有被其他會員使用
        if (emailChanged)
        {
            var emailExists = await _context.User
                .AnyAsync(u =>
                    u.UserId != userId &&
                    u.Email == dto.Email);

            if (emailExists)
            {
                return Conflict("此 Email 已被其他會員使用");
            }
        }
        // 3. 允許會員自己修改的欄位
        user.Username = dto.Username;

        if (emailChanged)
        {
            user.Email = dto.Email;

            // 新 Email 必須重新驗證
            user.EmailVerified = false;
        }

        user.Phone = dto.Phone;
        user.CountryCode = dto.CountryCode;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        // 4. 如果 Email 有變更，建立新的 Email 驗證 Token
        string? verificationToken = null;

        if (emailChanged)
        {
            // 讓舊的未使用 Token 失效
            var oldTokens = await _context.UserEmailVerificationToken
                .Where(t => t.UserId == userId && t.UsedAt == null)
                .ToListAsync();

            foreach (var token in oldTokens)
            {
                token.UsedAt = DateTimeOffset.UtcNow;
            }

            // 產生新的 Token
            verificationToken = _authTokenService.GenerateSecureToken();

            var tokenHash = _authTokenService.HashToken(verificationToken);

            var emailVerificationToken = new UserEmailVerificationToken
            {
                UserId = userId,
                TokenHash = tokenHash,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
            };

            _context.UserEmailVerificationToken.Add(emailVerificationToken);
        }

        // 5. 儲存資料
        await _context.SaveChangesAsync();

        // 6. Email 有修改才寄新的驗證信
        if (emailChanged && verificationToken != null)
        {
            var verificationLink =
                $"{frontendBaseUrl}/verify-email?token={Uri.EscapeDataString(verificationToken)}";

            await _emailVerificationService.SendVerificationEmailAsync(
                user.Email,
                verificationLink
            );
        }

        return NoContent();
    }

    // PUT: api/User/me/password
    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDTO dto)
    {
        // 1. 從 JWT 取得目前登入者 UserId
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        // 2. 從資料庫找到目前登入者
        var user = await _context.User
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
        {
            return NotFound();
        }

        if (string.IsNullOrEmpty(user.Password))
        {
            return BadRequest(
                "此帳號尚未設定密碼，請先設定密碼"
            );
        }

        // 3. 驗證目前密碼是否正確
        var passwordResult = _passwordHasher.VerifyHashedPassword(
            user,
            user.Password,
            dto.CurrentPassword
        );

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            return BadRequest("目前密碼錯誤");
        }

        // 4. 將新密碼轉成 Hash
        user.Password = _passwordHasher.HashPassword(
            user,
            dto.NewPassword
        );



        // 5. 儲存到資料庫 順便變更更新時間     
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // POST: api/User/me/password
    // 第三方登入會員首次設定密碼
    [HttpPost("me/password")]
    public async Task<IActionResult> SetPassword(SetPasswordDTO dto)
    {
        // 1. 從 JWT 取得目前登入者 UserId
        var userIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!int.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        // 2. 找目前登入會員
        var user = await _context.User
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
        {
            return NotFound();
        }


        // 3. 如果已經有密碼，就不能使用「首次設定密碼」
        if (!string.IsNullOrEmpty(user.Password))
        {
            return Conflict(
                "此帳號已設定密碼，請使用修改密碼功能"
            );
        }

        // 4. 將新密碼 Hash
        user.Password = _passwordHasher.HashPassword(
            user,
            dto.NewPassword
        );

        user.UpdatedAt = DateTimeOffset.UtcNow;

        // 5. 儲存
        await _context.SaveChangesAsync();

        return NoContent();
    }

}


