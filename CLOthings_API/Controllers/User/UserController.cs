using CLOthings_API.DTO.User;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public UserController(CLOthingsContext context)
    {
        _context = context;
    }

    // GET: api/User
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDTO>>> GetUser()
    {
        var users = await _context.User.Select(e => new UserDTO
        {
            UserId = e.UserId,
            Username = e.Username,
            Account = e.Account,
            Password = e.Password,
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
        var user = await _context.User
            .FirstOrDefaultAsync(u => u.Account == dto.Account && u.Password == dto.Password);

        if (user == null)
            return Unauthorized("帳號或密碼錯誤");

        return Ok("登入成功");
    }
}


