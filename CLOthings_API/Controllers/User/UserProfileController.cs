using CLOthings_API.DTOs;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CLOthings_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserProfileController : ControllerBase
    {
        private readonly CLOthingsContext _context;

        public UserProfileController(CLOthingsContext context)
        {
            _context = context;
        }


        // GET: api/UserProfile/me
        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult> GetMyProfile()
        {
            // 從 JWT 取得目前登入會員的 UserId
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdString, out var userId))
            {
                return Unauthorized();
            }

            // 找這個會員的 UserProfile
            var profile = await _context.UserProfile
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                return NotFound("尚未建立個人資料");
            }

            return Ok(new
            {
                profile.UserProfileId,
                profile.FirstName,
                profile.LastName,
                profile.Avatar,
                profile.Gender,
                profile.Birthday,
                profile.StyleTag,
                profile.Intro
            });
        }

        // PUT: api/UserProfile/me
        [Authorize]
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMyProfile(UserProfileDTO dto)
        {
            // 從 JWT 取得目前登入會員 UserId
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdString, out var userId))
            {
                return Unauthorized();
            }

            // 找目前會員的 Profile
            var profile = await _context.UserProfile
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                return NotFound("找不到個人資料");
            }

            // 更新資料
            profile.FirstName = dto.FirstName;
            profile.LastName = dto.LastName;
            profile.Avatar = dto.Avatar;
            profile.Gender = dto.Gender;
            profile.Birthday = dto.Birthday;
            profile.StyleTag = dto.StyleTag;
            profile.Intro = dto.Intro;

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}