namespace CLOthings_API.Models;

// NotificationDTO：一則通知要顯示在畫面上需要的資料，把觸發這則通知的人的名字、
// 大頭貼都先 join 好一起回傳，前端不用另外再查一次。
public class NotificationDTO
{
    public int NotificationId { get; set; }
    public string Type { get; set; } // 'follow' / 'like' / 'comment'
    public int FromUserId { get; set; }
    public string FromUsername { get; set; }
    public string? FromAvatar { get; set; }
    public int? CommunityPostId { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public bool IsRead { get; set; }
}