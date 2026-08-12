using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CLOthings_API.Models;

[Route("api/[controller]")]
[ApiController]
public class UserFollowController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public UserFollowController(CLOthingsContext context)
    {
        _context = context;
    }

    // GET: api/UserFollow/follower/3/following/5
    // 查詢「followerId 這個人，有沒有追蹤 followingId 這個人」，
    // PostDetailView.vue、UserProfileView.vue 的追蹤按鈕進頁面時要先問這個，
    // 才知道按鈕一開始要顯示「＋ 追蹤」還是「已追蹤」。
    // 有追蹤就回傳那筆 User_Follow 紀錄（帶著 userFollowId，之後要取消追蹤要用它），
    // 沒追蹤過就回傳 null。
    [HttpGet("follower/{followerid}/following/{followingid}")]
    public async Task<FollowDTO> GetUserFollow(int followerid, int followingid)
    {
        var follow = await _context.UserFollow
            .Where(f => f.FollowerId == followerid && f.FollowingId == followingid)
            .Select(f => new FollowDTO
            {
                UserFollowId = f.UserFollowId,
                FollowerId = f.FollowerId,
                FollowingId = f.FollowingId
            })
            .FirstOrDefaultAsync();

        if (follow == null)
        {
            return null;
        }

        return follow;
    }

    // GET: api/UserFollow/counts/5
    // 查「這個人的粉絲數／追蹤中數」，UserProfileView.vue 最上面那三個數字
    // （貼文、粉絲、追蹤中）裡的後兩個要用到。
    [HttpGet("counts/{userid}")]
    public async Task<FollowCountsDTO> GetFollowCounts(int userid)
    {
        // 粉絲數：User_Follow 裡 FollowingId 是我的列數（別人追蹤我）
        var followersCount = await _context.UserFollow.CountAsync(f => f.FollowingId == userid);
        // 追蹤中：User_Follow 裡 FollowerId 是我的列數（我追蹤別人）
        var followingCount = await _context.UserFollow.CountAsync(f => f.FollowerId == userid);

        return new FollowCountsDTO
        {
            FollowersCount = followersCount,
            FollowingCount = followingCount
        };
    }

    // POST: api/UserFollow
    // 追蹤：新增一筆 User_Follow 紀錄。
    [HttpPost]
    public async Task<ResultDTO> PostUserFollow(FollowDTO followDTO)
    {
        UserFollow follow = new UserFollow
        {
            UserFollowId = 0,
            FollowerId = followDTO.FollowerId,
            FollowingId = followDTO.FollowingId,
            FollowDate = DateTimeOffset.Now
        };
        _context.UserFollow.Add(follow);
        await _context.SaveChangesAsync();

        return new ResultDTO { OK = true, Code = 204 };
    }

    // DELETE: api/UserFollow/5
    // 取消追蹤：刪除那筆 User_Follow 紀錄，5 要帶 userFollowId
    // （從 GetUserFollow 查回來的那個 id）。
    [HttpDelete("{userfollowid}")]
    public async Task<ResultDTO> DeleteUserFollow(int? userfollowid)
    {
        var follow = await _context.UserFollow.FindAsync(userfollowid);
        if (follow == null)
        {
            return new ResultDTO { OK = false, Code = 404 };
        }
        try
        {
            _context.UserFollow.Remove(follow);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return new ResultDTO { OK = false, Code = 500 };
        }
        return new ResultDTO { OK = true, Code = 204 };
    }
}