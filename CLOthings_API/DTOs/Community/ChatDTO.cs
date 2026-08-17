namespace CLOthings_API.Models;

// ChatMessageDTO：一則訊息要顯示在畫面上需要的資料。
public class ChatMessageDTO
{
    public int ChatMessageId { get; set; }
    public int SenderId { get; set; }
    public int ReceiverId { get; set; }
    public string? Content { get; set; }
    public List<string> ImagePaths { get; set; } = new List<string>();
    public DateTimeOffset SentAt { get; set; }
    public bool IsRead { get; set; }
}

// ConversationDTO：對話清單（左側欄）一行要顯示的資料——對方是誰、
// 最後一則訊息內容／時間、有幾則還沒讀。
public class ConversationDTO
{
    public int OtherUserId { get; set; }
    public string OtherUsername { get; set; }
    public string OtherAvatar { get; set; }
    public string LastMessageContent { get; set; }
    public DateTimeOffset LastMessageDate { get; set; }
    public int UnreadCount { get; set; }
}

// SendMessageRequestDTO：前端呼叫 ChatHub.SendMessage 時帶的參數。
public class SendMessageRequestDTO
{
    public int ReceiverId { get; set; }
    public string? Content { get; set; }
    public List<string>? ImagePaths { get; set; }
}