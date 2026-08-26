using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CLOthings_API.Models;
using System.Security.Claims;

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

    // GET: api/UserFollow/followers/5
    // 查「這個人的粉絲名單」（誰追蹤了他），UserProfileView.vue 點粉絲數字彈出的名單要用這個。
    [HttpGet("followers/{userid}")]
    public async Task<IEnumerable<FollowUserDTO>> GetFollowers(int userid)
    {
        return await _context.UserFollow
            .Where(f => f.FollowingId == userid)
            .Select(f => new FollowUserDTO
            {
                UserId = f.Follower.UserId,
                Username = f.Follower.Username,
                Avatar = f.Follower.UserProfile.Select(p => p.Avatar).FirstOrDefault()
            })
            .ToListAsync();
    }

    // GET: api/UserFollow/following/5
    // 查「這個人追蹤中的名單」（他追蹤了誰），UserProfileView.vue 點追蹤中數字彈出的名單要用這個。
    [HttpGet("following/{userid}")]
    public async Task<IEnumerable<FollowUserDTO>> GetFollowing(int userid)
    {
        return await _context.UserFollow
            .Where(f => f.FollowerId == userid)
            .Select(f => new FollowUserDTO
            {
                UserId = f.Following.UserId,
                Username = f.Following.Username,
                Avatar = f.Following.UserProfile.Select(p => p.Avatar).FirstOrDefault()
            })
            .ToListAsync();
    }

    // GET: api/UserFollow/popular-creators?take=3&followerId=1
    // 找「粉絲數最多的前幾名」使用者，CommunityView.vue 側欄「熱門穿搭達人」要用這個。
    // take：要抓前幾名。followerId：目前登入的測試帳號 id，用來判斷這幾位「我」有沒有追蹤過，
    // 這樣清單裡每個人的追蹤按鈕才能一開始就顯示正確的狀態。
    [HttpGet("popular-creators")]
    public async Task<IEnumerable<CreatorDTO>> GetPopularCreators(int take, int followerId)
    {
        // 第一步：依 FollowingId 分組（每一組就是「某個人的所有粉絲」），
        // 算出每組有幾筆（= 這個人的粉絲數），依粉絲數由多到少排序，取前 take 名。
        var grouped = await _context.UserFollow
            .GroupBy(f => f.FollowingId)
            .Select(g => new { UserId = g.Key, FollowersCount = g.Count() })
            .OrderByDescending(g => g.FollowersCount)
            .Take(take)
            .ToListAsync();

        var userIds = grouped.Select(g => g.UserId).ToList();

        // 第二步：拿這幾個 userId，去 User 表查真正的名字、大頭貼。
        var users = await _context.User
            .Where(u => userIds.Contains(u.UserId))
            .Select(u => new
            {
                u.UserId,
                u.Username,
                Avatar = u.UserProfile.Select(p => p.Avatar).FirstOrDefault()
            })
            .ToListAsync();

        // 第三步：查「我」（followerId）已經追蹤了這幾個人裡的哪幾個，
        // 連 userFollowId 一起帶回來，之後要取消追蹤才不用另外再查一次。
        var myFollows = await _context.UserFollow
            .Where(f => f.FollowerId == followerId && userIds.Contains(f.FollowingId))
            .Select(f => new { f.FollowingId, f.UserFollowId })
            .ToListAsync();

        // 第四步：把上面三步的結果組合成最終要回傳的 CreatorDTO 清單。
        return grouped.Select(g =>
        {
            var user = users.FirstOrDefault(u => u.UserId == g.UserId);
            var myFollow = myFollows.FirstOrDefault(f => f.FollowingId == g.UserId);
            return new CreatorDTO
            {
                UserId = g.UserId,
                Name = user != null ? user.Username : "未知使用者",
                Avatar = user != null ? user.Avatar : null,
                FollowersCount = g.FollowersCount,
                IsFollowing = myFollow != null,
                UserFollowId = myFollow?.UserFollowId
            };
        });
    }

    // POST: api/UserFollow
    // 追蹤：新增一筆 User_Follow 紀錄。加 [Authorize]：追蹤一定要登入。
    //
    // 資安修正：原本直接相信前端 request body 裡的 followDTO.FollowerId，代表誰是「追蹤的人」——
    // 改成一律從登入用的 JWT Token 解出真正的身分，不管前端傳什麼都直接蓋掉。
    [HttpPost]
    [Authorize]
    public async Task<ResultDTO> PostUserFollow(FollowDTO followDTO)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return new ResultDTO { OK = false, Code = 401 };
        }

        UserFollow follow = new UserFollow
        {
            UserFollowId = 0,
            FollowerId = currentUserId.Value,
            FollowingId = followDTO.FollowingId,
            FollowDate = DateTimeOffset.Now
        };
        _context.UserFollow.Add(follow);
        await _context.SaveChangesAsync();

        // 追蹤成功後，順便通知被追蹤的那個人——正常情況下不會有人追蹤自己，
        // 但還是保守加個判斷，避免萬一發生時通知自己。
        //
        // 防重複通知：如果同一個人對同一個人「還沒讀」的追蹤通知已經存在，就不要再新增一筆——
        // 不然使用者取消追蹤又重新追蹤（不管是手滑還是真的反悔），通知會一直重複疊加，
        // 對方點開鈴鐺會看到好幾則一模一樣的「XXX 追蹤了你」。如果那則通知已經被讀過了，
        // 代表對方已經看過、事情算「翻頁」了，這次重新追蹤才會再產生一則新的通知。
        if (followDTO.FollowingId != currentUserId.Value)
        {
            var alreadyNotified = await _context.Notification.AnyAsync(n =>
                n.UserId == followDTO.FollowingId &&
                n.FromUserId == currentUserId.Value &&
                n.Type == "follow" &&
                !n.IsRead);

            if (!alreadyNotified)
            {
                _context.Notification.Add(new Notification
                {
                    UserId = followDTO.FollowingId,
                    FromUserId = currentUserId.Value,
                    Type = "follow",
                    CommunityPostId = null,
                    CreatedDate = DateTimeOffset.Now,
                    IsRead = false
                });
                await _context.SaveChangesAsync();
            }
        }

        return new ResultDTO { OK = true, Code = 204 };
    }

    // DELETE: api/UserFollow/5
    // 取消追蹤：刪除那筆 User_Follow 紀錄，5 要帶 userFollowId
    // （從 GetUserFollow 查回來的那個 id）。加 [Authorize]：取消追蹤一定要登入。
    //
    // 資安修正：原本只檢查「這筆紀錄存不存在」，沒檢查「這筆紀錄是不是登入者自己追蹤的」——
    // 加上比對 follow.FollowerId 是不是等於目前登入者的 userId。
    [HttpDelete("{userfollowid}")]
    [Authorize]
    public async Task<ResultDTO> DeleteUserFollow(int? userfollowid)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return new ResultDTO { OK = false, Code = 401 };
        }

        var follow = await _context.UserFollow.FindAsync(userfollowid);
        if (follow == null)
        {
            return new ResultDTO { OK = false, Code = 404 };
        }
        if (follow.FollowerId != currentUserId.Value)
        {
            return new ResultDTO { OK = false, Code = 403 };
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

    // GetCurrentUserId：跟 ChatController.cs 是同一套寫法，從登入用的 JWT Token 裡取出 userId，
    // 不相信前端自己送來的任何身分欄位。
    private int? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId) ? userId : null;
    }
}