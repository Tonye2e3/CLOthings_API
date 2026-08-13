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
    [Authorize]
    public class UserProfileController : ControllerBase
    {
        private readonly CLOthingsContext _context;

        public UserProfileController(CLOthingsContext context)
        {
            _context = context;
        }
        // 取得 JWT 裡目前登入會員的 UserId
        private bool TryGetCurrentUserId(out int userId)
        {
            var userIdString =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(userIdString, out userId);
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

        // POST: api/UserProfile/me/avatar
        // 上傳目前登入會員的頭像
        [HttpPost("me/avatar")]
        public async Task<ActionResult> UploadAvatar(IFormFile file)
        {
            // 1. 從 JWT 取得目前登入會員 UserId
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            // 2. 檢查有沒有收到檔案
            if (file == null || file.Length == 0)
            {
                return BadRequest("請選擇圖片");
            }

            // 3. 限制檔案大小：5 MB
            if (file.Length > 5 * 1024 * 1024)
            {
                return BadRequest("圖片大小不能超過 5 MB");
            }

            // 4. 限制圖片格式
            var allowedExtensions = new[]
            {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest("只允許 JPG、JPEG、PNG、WEBP 圖片");
            }

            // 5. 找目前會員的 UserProfile
            var profile = await _context.UserProfile
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                return NotFound("尚未建立個人資料");
            }

            // 6. 產生新的唯一檔名
            var fileName =
                $"avatar_{userId}_{Guid.NewGuid():N}{extension}";

            // 7. 實體儲存位置
            var folderPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "images",
                "user",
                "avatars"
            );

            // 保險：資料夾不存在就建立
            Directory.CreateDirectory(folderPath);

            var filePath = Path.Combine(folderPath, fileName);

            // 8. 儲存圖片
            using (var stream = new FileStream(
                filePath,
                FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 9. 資料庫只儲存圖片網址
            var avatarUrl =
                $"/images/user/avatars/{fileName}";

            profile.Avatar = avatarUrl;

            await _context.SaveChangesAsync();

            // 10. 回傳給前端
            return Ok(new
            {
                avatar = avatarUrl
            });

        }
    }
}