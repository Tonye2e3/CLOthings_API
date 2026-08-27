using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CLOthings_API.Models;

// NotificationController：負責「查詢」通知用的一般 API——列出通知清單、算未讀數量、
// 標記已讀。真正「產生」通知的動作，分散寫在 PostLikeController.cs（按讚）、
// PostCommentController.cs（留言）、UserFollowController.cs（追蹤）那三支各自的
// POST 方法裡，各自的動作發生時，自己順便新增一筆 Notification，不集中在這裡處理。
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public NotificationController(CLOthingsContext context)
    {
        _context = context;
    }

    // GET: api/Notification/user/5
    // 查「這個使用者最近的通知」，依時間新到舊排序，最新的通知排最前面。
    // 通知鈴鐺的下拉清單要用這支。
    //
    // 資安修正：原本網址上的 {userid} 是「想查誰的通知」，但沒有跟登入者本人比對——
    // 只要有登入（不管是誰），把網址換成別人的 userid 就能看到別人的通知內容
    // （誰追蹤/按讚/留言了對方），這是私人資訊，不該讓其他登入使用者查得到。
    // 改成不管網址上寫什麼 userid，一律只查登入者自己的通知，網址上的 {userid}
    // 保留只是為了不用改前端呼叫的網址格式，實際查詢一律用 Token 解出來的身分。
    //
    // 只取最新 20 則：這支是給「通知鈴鐺的下拉選單」用的，用途是快速瞄一眼最近的動態
    [HttpGet("user/{userid}")]
    public async Task<IEnumerable<NotificationDTO>> GetNotificationsByUser(int userid)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return new List<NotificationDTO>();
        }

        return await _context.Notification
            .Where(n => n.UserId == currentUserId.Value)
            .OrderByDescending(n => n.CreatedDate)
            .Take(20)
            .Select(n => new NotificationDTO
            {
                NotificationId = n.NotificationId,
                Type = n.Type,
                FromUserId = n.FromUserId,
                FromUsername = n.FromUser.Username,
                FromAvatar = n.FromUser.UserProfile.Select(p => p.Avatar).FirstOrDefault(),
                CommunityPostId = n.CommunityPostId,
                CreatedDate = n.CreatedDate,
                IsRead = n.IsRead
            })
            .ToListAsync();
    }

    // GET: api/Notification/unread-count/5
    // 查「這個使用者有幾則還沒讀的通知」，鈴鐺圖示上面的小紅點數字要用這支。
    // 一樣改成只看登入者自己的未讀數量，理由跟上面 GetNotificationsByUser 一樣。
    [HttpGet("unread-count/{userid}")]
    public async Task<int> GetUnreadCount(int userid)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return 0;
        }
        return await _context.Notification.CountAsync(n => n.UserId == currentUserId.Value && !n.IsRead);
    }

    // PUT: api/Notification/mark-all-read/5
    // 把「畫面上實際顯示出來的」那些通知標記成已讀——點開通知鈴鐺清單的時候呼叫，
    // 比一則一則個別標記已讀簡單，對使用者來說「打開清單＝看過了」也是合理的行為。
    //
    // 只標記最新 20 則：跟 GetNotificationsByUser 那支「只回傳最新 20 則」互相搭配——
    // 如果未讀數超過 20，使用者只看得到最新 20 則，剩下沒顯示出來的那些不該被標記已讀
    // （沒看到卻被當成看過了），所以這裡也只標記畫面上真正顯示出來的那 20 則。
    [HttpPut("mark-all-read/{userid}")]
    public async Task<ResultDTO> MarkAllAsRead(int userid)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return new ResultDTO { OK = false, Code = 401 };
        }

        var visibleUnread = await _context.Notification
            .Where(n => n.UserId == currentUserId.Value)
            .OrderByDescending(n => n.CreatedDate)
            .Take(20)
            .Where(n => !n.IsRead)
            .ToListAsync();

        foreach (var n in visibleUnread)
        {
            n.IsRead = true;
        }
        await _context.SaveChangesAsync();

        return new ResultDTO { OK = true, Code = 204 };
    }

    // GetCurrentUserId：跟其他 Controller 是同一套寫法，從登入用的 JWT Token 裡取出 userId，
    // 不相信網址上的 {userid} 參數。
    private int? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId) ? userId : null;
    }
}