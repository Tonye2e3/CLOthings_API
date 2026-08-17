using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CLOthings_API.Models;

// ChatController：跟 ChatHub.cs 是同一個聊天室功能的兩個不同部分——
// Hub 負責「即時」的部分（傳送新訊息、馬上推給對方）；
// 這支 Controller 負責「一般 API」的部分（打開聊天室時，先把過去的對話歷史、
// 對話清單這種「一次性查詢」的資料抓回來，這種資料不需要即時通道，用一般 API 就好）。
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public ChatController(CLOthingsContext context)
    {
        _context = context;
    }

    // GET: api/Chat/conversations
    // 左側「對話清單」要用的資料：跟我聊過天的每一個人，各自的最後一則訊息、還沒讀的則數。
    [HttpGet("conversations")]
    public async Task<IEnumerable<ConversationDTO>> GetConversations()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return new List<ConversationDTO>();

        // 抓出「跟我有關」的所有訊息（我傳的或我收的）。對話數量通常不會太多，
        // 直接整批抓回來、在記憶體裡用 GroupBy 分組處理，比硬寫一次到位的複雜 SQL 好懂多了。
        var myMessages = await _context.ChatMessage
            .Where(m => m.SenderId == userId || m.ReceiverId == userId)
            .OrderByDescending(m => m.SentAt)
            .ToListAsync();

        // 分組的 key 是「對方是誰」：如果這則是我傳的，對方就是 ReceiverId；
        // 如果這則是我收的，對方就是 SenderId。
        var grouped = myMessages
            .GroupBy(m => m.SenderId == userId ? m.ReceiverId : m.SenderId)
            .Select(g => new
            {
                OtherUserId = g.Key,
                // myMessages 已經照時間新到舊排過序了，所以每組的第一筆就是這段對話最新的一則。
                Last = g.First(),
                UnreadCount = g.Count(m => m.ReceiverId == userId && !m.IsRead)
            })
            .OrderByDescending(g => g.Last.SentAt)
            .ToList();

        var otherUserIds = grouped.Select(g => g.OtherUserId).ToList();
        var otherUsers = await _context.User
            .Where(u => otherUserIds.Contains(u.UserId))
            .Select(u => new
            {
                u.UserId,
                u.Username,
                Avatar = u.UserProfile.Select(p => p.Avatar).FirstOrDefault()
            })
            .ToListAsync();

        return grouped.Select(g =>
        {
            var other = otherUsers.FirstOrDefault(u => u.UserId == g.OtherUserId);
            return new ConversationDTO
            {
                OtherUserId = g.OtherUserId,
                OtherUsername = other?.Username,
                OtherAvatar = other?.Avatar,
                LastMessageContent = g.Last.Content,
                LastMessageDate = g.Last.SentAt,
                UnreadCount = g.UnreadCount
            };
        });
    }

    // GET: api/Chat/messages/{otherUserId}
    // 跟某一個人之間完整的對話歷史（照時間舊到新排，畫面上由上往下就是對話順序）。
    // 呼叫這支的同時，會順便把「對方傳給我、我還沒讀過」的訊息標記成已讀——
    // 使用者打開這段對話串，就代表看到了，不用前端另外再呼叫一支「標記已讀」API。
    [HttpGet("messages/{otherUserId}")]
    public async Task<IEnumerable<ChatMessageDTO>> GetMessages(int otherUserId)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return new List<ChatMessageDTO>();

        var messages = await _context.ChatMessage
            .Where(m => (m.SenderId == userId && m.ReceiverId == otherUserId) ||
                        (m.SenderId == otherUserId && m.ReceiverId == userId))
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        var unread = messages.Where(m => m.ReceiverId == userId && !m.IsRead).ToList();
        if (unread.Count > 0)
        {
            foreach (var m in unread) m.IsRead = true;
            await _context.SaveChangesAsync();
        }

        return messages.Select(m => new ChatMessageDTO
        {
            ChatMessageId = m.ChatMessageId,
            SenderId = m.SenderId,
            ReceiverId = m.ReceiverId,
            Content = m.Content,
            SentAt = m.SentAt,
            IsRead = m.IsRead
        });
    }

    // GetCurrentUserId：跟其他 Controller（例如 UserController.cs）同一套寫法，
    // 從登入用的 JWT Token 裡取出 userId。回傳 int? 是因為理論上 Token 解析失敗時
    // （不應該發生，[Authorize] 已經先擋掉沒登入的請求）要有辦法回傳「沒有」。
    private int? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId) ? userId : null;
    }
}