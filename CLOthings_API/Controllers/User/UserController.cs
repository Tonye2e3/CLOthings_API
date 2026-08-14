using CLOthings.Enums;
using CLOthings_API.DTO.User;
using CLOthings_API.DTOs;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;


[Route("api/[controller]")]
[ApiController]
[Authorize]
public class UserController : ControllerBase
{
    private readonly CLOthingsContext _context;
    private readonly IConfiguration _configuration;
    private readonly IPasswordHasher<User> _passwordHasher;

    public UserController(CLOthingsContext context, IConfiguration configuration, IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _configuration = configuration;
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
    [HttpGet("{userid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<User>> GetUser(int userid)
    {
        var user = await _context.User.FindAsync(userid);

        if (user == null)
        {
            return NotFound();
        }

        return user;
    }

    // PUT: api/User/5
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPut("{userid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> PutUser(int? userid, User user)
    {
        if (userid != user.UserId)
        {
            return BadRequest();
        }

        _context.Entry(user).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!UserExists(userid))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

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

    private string GenerateAccessToken(User user)
    {
        // 取得會員角色
        var role = ((UserTypeEnum)user.UserType).ToString();

        // JWT 裡要存放的會員資訊
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

        // 取得 appsettings.json 裡面的 JWT Key
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                _configuration["Jwt:Key"]!
            )
        );

        // 使用 HMAC SHA256 簽章
        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256
        );

        // 建立 JWT
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,

            // 先維持你目前的 2 小時
            expires: DateTime.UtcNow.AddHours(2),

            signingCredentials: credentials
        );

        // JWT 物件轉成字串
        return new JwtSecurityTokenHandler()
            .WriteToken(token);
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

        // 5. 儲存到資料庫
        await _context.SaveChangesAsync();

        return NoContent();
    }


}


