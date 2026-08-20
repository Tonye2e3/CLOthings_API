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
                // 最後一則訊息如果是純圖片（Content 是 null），左側清單預覽文字
                // 顯示「[圖片]」，不要留白讓使用者以為清單資料是空的。
                LastMessageContent = g.Last.Content ?? "[圖片]",
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
            .Include(m => m.ChatMessageImage)
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
            ImagePaths = BuildImagePaths(m),
            SentAt = m.SentAt,
            IsRead = m.IsRead
        });
    }

    // POST: api/Chat/upload-image
    // 跟 CommunityPostController.cs 的 upload-images 是同一套邏輯（存進 wwwroot、
    // 檔名用 Guid 避免撞名，可以一次上傳多個檔案），只是存到不同的資料夾
    // （images/chat 而不是 images/posts），圖片分開放，之後如果要清理／備份聊天圖片，
    // 不會跟貼文圖片混在一起。
    //
    // 傳送圖片訊息的流程是「先上傳、再送出」：前端選好圖片後先打這支 API 把檔案存到伺服器、
    // 拿到路徑清單，接著才呼叫 ChatHub.SendMessage 把這些路徑存進訊息裡——
    // WebSocket（Hub）不適合直接拿來傳檔案本身，檔案上傳還是走一般的 HTTP API 比較單純。
    [HttpPost("upload-image")]
    public async Task<ActionResult<List<string>>> UploadImage(List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest();
        }

        var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "chat");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var savedPaths = new List<string>();

        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                continue;
            }

            var extension = Path.GetExtension(file.FileName);
            var newFileName = $"{Guid.NewGuid()}{extension}";
            var fullPath = Path.Combine(folder, newFileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            savedPaths.Add($"/images/chat/{newFileName}");
        }

        return Ok(savedPaths);
    }

    // GetCurrentUserId：跟其他 Controller（例如 UserController.cs）同一套寫法，
    // 從登入用的 JWT Token 裡取出 userId。回傳 int? 是因為理論上 Token 解析失敗時
    // （不應該發生，[Authorize] 已經先擋掉沒登入的請求）要有辦法回傳「沒有」。
    private int? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId) ? userId : null;
    }

    // BuildImagePaths：把一則訊息的圖片整理成統一的陣列格式——
    // 新訊息的圖片存在 ChatMessageImage（可能好幾張，照 SortOrder 排序）；
    // 舊訊息（在支援多圖之前傳的）圖片存在 ChatMessage.ImagePath 這個舊欄位（只有一張）。
    // 前端畫面只需要處理 ImagePaths 這一種陣列格式，不用知道背後這兩種不同的存法。
    private static List<string> BuildImagePaths(ChatMessage m)
    {
        if (m.ChatMessageImage != null && m.ChatMessageImage.Count > 0)
        {
            return m.ChatMessageImage
                .OrderBy(i => i.SortOrder)
                .Select(i => i.ImagePath)
                .ToList();
        }
        if (!string.IsNullOrWhiteSpace(m.ImagePath))
        {
            return new List<string> { m.ImagePath };
        }
        return new List<string>();
    }
}