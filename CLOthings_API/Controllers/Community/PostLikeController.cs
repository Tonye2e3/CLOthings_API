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
    [HttpPost]
    [Authorize]
    public async Task<ResultDTO> PostPostLike(LikeDTO likeDTO)
    {
        PostLike like = new PostLike
        {
            PostLikesId = 0,
            CommunityPostId = likeDTO.CommunityPostId,
            UserId = likeDTO.UserId,
            LikeDate = DateTimeOffset.Now
        };
        _context.PostLike.Add(like);
        await _context.SaveChangesAsync();

        return new ResultDTO { OK = true, Code = 204 };
    }

    // DELETE: api/PostLike/5
    // 取消讚：刪除那筆 Post_Like 紀錄，5 要帶 postLikesId（從 GetPostLikeByPostAndUser 查回來的那個 id）。
    // 加 [Authorize]：取消讚一定要登入。
    [HttpDelete("{postlikesid}")]
    [Authorize]
    public async Task<ResultDTO> DeletePostLike(int? postlikesid)
    {
        var like = await _context.PostLike.FindAsync(postlikesid);
        if (like == null)
        {
            return new ResultDTO { OK = false, Code = 404 };
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
}