using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CLOthings_API.Models;

[Route("api/[controller]")]
[ApiController]
public class PostLikeController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public PostLikeController(CLOthingsContext context)
    {
        _context = context;
    }

    // GET: api/PostLike/post/5/user/3
    // 查詢「這個使用者有沒有幫這篇貼文按過讚」，PostDetailView.vue 進頁面時要先問這個，
    // 才知道愛心圖示一開始要顯示成「已按讚」還是「還沒按讚」。
    // 有按過讚就回傳那筆 Post_Like 紀錄（帶著 postLikesId，之後要取消讚要用它），
    // 沒按過就回傳 null。
    [HttpGet("post/{communitypostid}/user/{userid}")]
    public async Task<LikeDTO> GetPostLikeByPostAndUser(int communitypostid, int userid)
    {
        var like = await _context.PostLike
            .Where(l => l.CommunityPostId == communitypostid && l.UserId == userid)
            .Select(l => new LikeDTO
            {
                PostLikesId = l.PostLikesId,
                CommunityPostId = l.CommunityPostId,
                UserId = l.UserId,
                LikeDate = l.LikeDate
            })
            .FirstOrDefaultAsync();

        if (like == null)
        {
            return null;
        }

        return like;
    }

    // GET: api/PostLike/user/3
    // 查詢「這個使用者總共讚過哪些貼文」，CommunityView.vue 一次要顯示一整面的貼文卡片，
    // 不會一篇一篇單獨去問（那樣要打幾十次 API），改成一次把這個使用者按過的所有讚都抓回來，
    // 前端自己在畫面上比對每張卡片是不是在這份清單裡。
    [HttpGet("user/{userid}")]
    public async Task<List<LikeDTO>> GetPostLikesByUser(int userid)
    {
        return await _context.PostLike
            .Where(l => l.UserId == userid)
            .Select(l => new LikeDTO
            {
                PostLikesId = l.PostLikesId,
                CommunityPostId = l.CommunityPostId,
                UserId = l.UserId,
                LikeDate = l.LikeDate
            })
            .ToListAsync();
    }

    // POST: api/PostLike
    // 按讚：新增一筆 Post_Like 紀錄。加 [Authorize]：按讚一定要登入。
    //
    // 資安修正：原本這裡直接相信前端 request body 裡的 likeDTO.UserId，代表誰是「按讚的人」——
    // 但只要有登入（拿得到合法 Token），就能自己改前端送出的內容，冒充任何 userId 幫別人按讚。
    // 改成一律從登入用的 JWT Token 解出真正的身分（GetCurrentUserId()），
    // 不管前端 request body 裡帶的 UserId 是什麼都直接忽略、蓋掉，
    // 這樣「按讚的人是誰」只有伺服器自己說了算，前端沒辦法偽造。
    [HttpPost]
    [Authorize]
    public async Task<ResultDTO> PostPostLike(LikeDTO likeDTO)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return new ResultDTO { OK = false, Code = 401 };
        }

        PostLike like = new PostLike
        {
            PostLikesId = 0,
            CommunityPostId = likeDTO.CommunityPostId,
            UserId = currentUserId.Value,
            LikeDate = DateTimeOffset.Now
        };
        _context.PostLike.Add(like);
        await _context.SaveChangesAsync();

        // 按讚成功後，順便通知這篇貼文的作者——除非「自己讚自己的貼文」，
        // 那種情況不用通知自己。要先查出這篇貼文是誰發的才知道通知要給誰。
        var post = await _context.CommunityPost.FindAsync(likeDTO.CommunityPostId);
        if (post != null && post.UserId != currentUserId.Value)
        {
            _context.Notification.Add(new Notification
            {
                UserId = post.UserId,
                FromUserId = currentUserId.Value,
                Type = "like",
                CommunityPostId = likeDTO.CommunityPostId,
                CreatedDate = DateTimeOffset.Now,
                IsRead = false
            });
            await _context.SaveChangesAsync();
        }

        return new ResultDTO { OK = true, Code = 204 };
    }

    // DELETE: api/PostLike/5
    // 取消讚：刪除那筆 Post_Like 紀錄，5 要帶 postLikesId（從 GetPostLikeByPostAndUser 查回來的那個 id）。
    // 加 [Authorize]：取消讚一定要登入。
    //
    // 資安修正：原本這裡只檢查「這筆紀錄存不存在」，沒檢查「這筆紀錄是不是登入者自己按的」——
    // 任何登入的人只要猜到（或照順序試）postlikesid，就能刪掉別人的讚。
    // 加上比對 like.UserId 是不是等於目前登入者的 userId，不是自己的就直接擋掉（403）。
    [HttpDelete("{postlikesid}")]
    [Authorize]
    public async Task<ResultDTO> DeletePostLike(int? postlikesid)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return new ResultDTO { OK = false, Code = 401 };
        }

        var like = await _context.PostLike.FindAsync(postlikesid);
        if (like == null)
        {
            return new ResultDTO { OK = false, Code = 404 };
        }
        if (like.UserId != currentUserId.Value)
        {
            return new ResultDTO { OK = false, Code = 403 };
        }
        try
        {
            _context.PostLike.Remove(like);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return new ResultDTO { OK = false, Code = 500 };
        }
        return new ResultDTO { OK = true, Code = 204 };
    }

    // GetCurrentUserId：跟 ChatController.cs 是同一套寫法，從登入用的 JWT Token 裡取出 userId，
    // 不相信前端自己送來的任何身分欄位。
    private int? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId) ? userId : null;
    }
}