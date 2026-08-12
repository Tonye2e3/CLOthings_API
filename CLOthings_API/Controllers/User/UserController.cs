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
    [Authorize(Roles = "Admin,SuperAdmin")]
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
    public async Task<ActionResult<User>> PostUser(User user)
    {
        // 將使用者輸入的密碼轉成 Hash
        user.Password = _passwordHasher.HashPassword(user, user.Password);

        // 存入資料庫
        _context.User.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction("GetUser", new { userid = user.UserId }, user);
    }

    // DELETE: api/User/5
    [HttpDelete("{userid}")]
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



    // POST: api/User/login
    [HttpPost("login")]
    public async Task<ActionResult> Login(LoginDTO dto)
    {
        // 先根據帳號找使用者
        var user = await _context.User
            .FirstOrDefaultAsync(u => u.Account == dto.Account);

        if (user == null)
            return Unauthorized("帳號或密碼錯誤");

        // 驗證使用者輸入的密碼是否符合資料庫中的 Password Hash
        var passwordResult = _passwordHasher.VerifyHashedPassword(
            user,
            user.Password,
            dto.Password
        );

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized("帳號或密碼錯誤");
        }

        // 後面 JWT...
        var role = ((UserTypeEnum)user.UserType).ToString();




        var claims = new[]
            {
            new Claim(ClaimTypes.NameIdentifier,user.UserId.ToString()),
            new Claim(ClaimTypes.Name,user.Username),
            new Claim("account",user.Account),
            new Claim(ClaimTypes.Role,role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new
        {
            token = tokenString,
            name = user.Username,
            account = user.Account,
            role = ((UserTypeEnum)user.UserType).ToString()
        }
            );
    }

    // GET: api/User/me
    [HttpGet("me")]
    [Authorize]
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
    [Authorize]
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
    [Authorize]
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

    // ⚠️ 開發階段暫時使用，成功後立刻刪除
    [HttpPost("reset-superadmin-password")]
    public async Task<IActionResult> ResetSuperAdminPassword()
    {
        var user = await _context.User
            .FirstOrDefaultAsync(u => u.Account == "superAdmin");

        if (user == null)
        {
            return NotFound("找不到 superAdmin");
        }

        // 暫時設定一組你知道的密碼
        var newPassword = "superAdmin";

        // 新密碼轉成 Hash
        user.Password = _passwordHasher.HashPassword(
            user,
            newPassword
        );

        await _context.SaveChangesAsync();

        return Ok("superAdmin 密碼已重設");
    }
}


