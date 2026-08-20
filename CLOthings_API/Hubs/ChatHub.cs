using System.Security.Claims;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using CLOthings_API.Models;

namespace CLOthings_API.Hubs;

// ChatHub：WebSocket 即時通道，跟一般 Controller 不一樣——Controller 是「前端問一次、
// 後端答一次」，Hub 是雙方可以隨時互推訊息，不用前端一直重複發請求問「有新訊息嗎？」。
//
// [Authorize]：只有帶著合法登入 Token 的使用者才能連上這個 Hub。要注意的是，瀏覽器的
// WebSocket 連線沒辦法像一般 API 那樣夾帶 Authorization 標頭，所以前端 SignalR 用戶端
// 是把 Token 放在網址的查詢字串（?access_token=xxx），要搭配 Program.cs 裡
// JwtBearerOptions.Events.OnMessageReceived 那段設定，後端才讀得到這個 Token。
[Authorize]
public class ChatHub : Hub
{
    private readonly CLOthingsContext _context;

    public ChatHub(CLOthingsContext context)
    {
        _context = context;
    }

    // SendMessage：前端呼叫 connection.invoke("SendMessage", { receiverId, content }) 時執行。
    // 存進資料庫、再即時推給「接收方」跟「自己」（自己也要收一份是因為同一個帳號可能同時
    // 開好幾個分頁或裝置，都要同步顯示剛剛送出的這則訊息）。
    public async Task SendMessage(SendMessageRequestDTO request)
    {
        // Context.User：SignalR 從連線用的 JWT Token 解析出來的身分資訊，
        // 跟一般 Controller 裡的 User（HttpContext.User）是同一套 Claims，
        // 這裡一樣是用 ClaimTypes.NameIdentifier 取出登入者的 userId。
        var senderIdValue = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (senderIdValue == null || !int.TryParse(senderIdValue, out var senderId))
        {
            return;
        }

        var content = request.Content?.Trim();
        // 圖片路徑陣列先把 null／空字串濾掉，也去掉頭尾多餘空白，得到一份「乾淨」的清單。
        var imagePaths = (request.ImagePaths ?? new List<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        // 文字、圖片至少要有一個：兩個都是空的話，代表前端傳來一則「什麼都沒有」的訊息，
        // 直接擋掉，不要存進資料庫。
        if (string.IsNullOrWhiteSpace(content) && imagePaths.Count == 0)
        {
            return;
        }

        var message = new ChatMessage
        {
            SenderId = senderId,
            ReceiverId = request.ReceiverId,
            Content = string.IsNullOrWhiteSpace(content) ? null : content,
            SentAt = DateTimeOffset.Now,
            IsRead = false
        };

        // 圖片存進 ChatMessageImage 這張新表，一張圖一筆資料、SortOrder 記住順序——
        // ChatMessage.ImagePath 這個舊欄位不再使用（保留給以前存的舊訊息讀取用）。
        for (int i = 0; i < imagePaths.Count; i++)
        {
            message.ChatMessageImage.Add(new ChatMessageImage
            {
                ImagePath = imagePaths[i],
                SortOrder = i
            });
        }

        _context.ChatMessage.Add(message);
        await _context.SaveChangesAsync();

        var dto = new ChatMessageDTO
        {
            ChatMessageId = message.ChatMessageId,
            SenderId = message.SenderId,
            ReceiverId = message.ReceiverId,
            Content = message.Content,
            ImagePaths = imagePaths,
            SentAt = message.SentAt,
            IsRead = message.IsRead
        };

        // Clients.User(userId)：SignalR 內建的功能，把訊息推給「這個 userId 名下所有連線」
        // （不管對方開了幾個分頁/裝置，全部都會收到）。這裡能直接用 userId 字串比對，
        // 是因為 Program.cs 的 JWT 設定裡，userId 放在 ClaimTypes.NameIdentifier，
        // 剛好是 SignalR 預設拿來判斷「這條連線屬於哪個使用者」的欄位，不用另外寫設定。
        //
        // 如果接收方現在沒有開著聊天室（沒有連線），這裡就等於「傳給空氣」，不會出錯，
        // 但訊息已經確實存進資料庫了——對方之後打開聊天室，用 REST API 撈歷史訊息時，
        // 一樣看得到這則，只是不是「即時跳出來」而已。
        await Clients.User(request.ReceiverId.ToString()).SendAsync("ReceiveMessage", dto);
        await Clients.User(senderId.ToString()).SendAsync("ReceiveMessage", dto);
    }
}