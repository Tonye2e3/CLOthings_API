using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CLOthings_API.Models;

[Route("api/[controller]")]
[ApiController]
public class PostReportController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public PostReportController(CLOthingsContext context)
    {
        _context = context;
    }

    // POST: api/PostReport
    // 檢舉一篇貼文。加 [Authorize]：檢舉一定要登入（不然無法判斷是誰檢舉的，
    // 也沒辦法擋掉「同一人重複檢舉同一篇」）。
    //
    // 資安修正：原本直接相信前端 request body 裡的 reportDTO.ReporterId，代表誰是「檢舉的人」——
    // 改成一律從登入用的 JWT Token 解出真正的身分，不管前端傳什麼都直接蓋掉，
    // 不然有心人士可以冒充別人的 userId 亂檢舉，甚至用來鎖住別人的「一人一次」檢舉額度。
    [HttpPost]
    [Authorize]
    public async Task<ResultDTO> PostPostReport(PostReportDTO reportDTO)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return new ResultDTO { OK = false, Code = 401 };
        }

        // 先看這個人是不是已經檢舉過這篇貼文了——UQ_PostReport_Post_Reporter 這個唯一約束
        // 其實資料庫層也會擋掉，但先在這裡查一次，才能回傳一個清楚的訊息給前端顯示，
        // 而不是讓前端收到一個看不懂的資料庫錯誤。
        var existing = await _context.PostReport
            .FirstOrDefaultAsync(r => r.CommunityPostId == reportDTO.CommunityPostId && r.ReporterId == currentUserId.Value);
        if (existing != null)
        {
            return new ResultDTO { OK = false, Code = 409 }; // 409：已經檢舉過了
        }

        var report = new PostReport
        {
            CommunityPostId = reportDTO.CommunityPostId,
            ReporterId = currentUserId.Value,
            Reason = reportDTO.Reason,
            CreatedDate = DateTimeOffset.Now
        };
        _context.PostReport.Add(report);
        await _context.SaveChangesAsync();

        return new ResultDTO { OK = true, Code = 204 };
    }

    // GET: api/PostReport/summary
    // 後台用的：列出「每篇被檢舉過的貼文」跟各自的檢舉次數，依次數由多到少排序。
    // AdminCommunityPostListView.vue 可以用這支在列表上額外顯示一欄「檢舉次數」，
    // 方便管理員優先處理檢舉數比較多的貼文。
    //
    // 資安修正：原本這支沒加任何 [Authorize]，任何人（不用登入）都能看到「哪些貼文
    // 被檢舉過幾次」——這是後台管理用的資訊，補上限管理員才能查詢。
    [HttpGet("summary")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IEnumerable<PostReportSummaryDTO>> GetReportSummary()
    {
        return await _context.PostReport
            .GroupBy(r => r.CommunityPostId)
            .Select(g => new PostReportSummaryDTO
            {
                CommunityPostId = g.Key,
                ReportCount = g.Count()
            })
            .OrderByDescending(s => s.ReportCount)
            .ToListAsync();
    }

    // GET: api/PostReport/post/5
    // 查某一篇貼文完整的檢舉紀錄（誰檢舉的、原因是什麼），
    // AdminCommunityPostDetailView.vue 點進單篇貼文的詳情頁時可以用這支顯示明細。
    //
    // 資安修正：原本這支也沒加 [Authorize]——比 summary 那支問題更大，這支會直接洩漏
    // 「是誰檢舉的」（ReporterId）跟檢舉原因，任何人都查得到，等於檢舉者的身分完全不保密。
    // 補上限管理員才能查詢，一般使用者、訪客都不該看到這份明細。
    [HttpGet("post/{communitypostid}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IEnumerable<PostReportDTO>> GetReportsByPost(int communitypostid)
    {
        return await _context.PostReport
            .Where(r => r.CommunityPostId == communitypostid)
            .OrderByDescending(r => r.CreatedDate)
            .Select(r => new PostReportDTO
            {
                PostReportId = r.PostReportId,
                CommunityPostId = r.CommunityPostId,
                ReporterId = r.ReporterId,
                Reason = r.Reason,
                CreatedDate = r.CreatedDate
            })
            .ToListAsync();
    }

    // GetCurrentUserId：跟 ChatController.cs 是同一套寫法，從登入用的 JWT Token 裡取出 userId，
    // 不相信前端自己送來的任何身分欄位。
    private int? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId) ? userId : null;
    }
}