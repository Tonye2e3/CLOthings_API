using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CLOthings_API.Models;

// 這個 Controller 先只做「查詢任何人的公開基本資料」（暱稱、帳號、大頭貼、風格標籤），
// 因為隊友的 UserProfileController 目前只有 GET api/UserProfile/me（只能查自己）。
// 如果之後隊友的 API 有查別人的公開版本，這個檔案就可以整個刪掉、換成那個。
[Route("api/[controller]")]
[ApiController]
public class PublicUserProfileController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public PublicUserProfileController(CLOthingsContext context)
    {
        _context = context;
    }

    // GET: api/PublicUserProfile/5
    // 不用登入，任何人瀏覽別人的個人頁都該看得到這幾個基本資訊。
    [HttpGet("{userid}")]
    public async Task<ActionResult<PublicUserProfileDTO>> GetPublicProfile(int userid)
    {
        var profile = await _context.User
            .Where(u => u.UserId == userid)
            .Select(u => new PublicUserProfileDTO
            {
                UserId = u.UserId,
                Username = u.Username,
                Account = u.Account,
                Avatar = u.UserProfile.Select(p => p.Avatar).FirstOrDefault(),
                StyleTag = u.UserProfile.Select(p => p.StyleTag).FirstOrDefault(),
                Intro = u.UserProfile.Select(p => p.Intro).FirstOrDefault()
            })
            .FirstOrDefaultAsync();

        if (profile == null)
        {
            return NotFound();
        }

        return Ok(profile);
    }
}