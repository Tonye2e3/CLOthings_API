using CLOthings.Enums;
using CLOthings_API.DTO.User;
using CLOthings_API.DTOs;
using CLOthings_API.Models;
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

    public UserController(CLOthingsContext context, IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;

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

    // POST: api/User
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult> PostUser(RegisterDTO dto)
    {
        // 先檢查帳號是否已存在
        var accountExists = await _context.User
            .AnyAsync(u => u.Account == dto.Account);

        if (accountExists)
        {
            return Conflict("帳號已存在");
        }

        // 🟢檢查 Email 是否已存在
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

            // 後端決定，不相信前端
            UserType = (int)UserTypeEnum.User,
            Status = (int)StatusEnum.Active,

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
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetUser),
            new { userid = user.UserId },
            new
            {
                userId = user.UserId,
                username = user.Username,
                account = user.Account,
                email = user.Email,
                phone = user.Phone
            }
        );
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
            phone = user.Phone,
            countryCode = user.CountryCode,
            twoFactorEnabled = user.TwoFactorEnabled
        });
    }

    // PUT: api/User/me
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

        // 3. 修改允許會員自己修改的欄位
        user.Username = dto.Username;
        user.Email = dto.Email;
        user.Phone = dto.Phone;
        user.CountryCode = dto.CountryCode;

        // 4. 儲存到資料庫
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();

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


