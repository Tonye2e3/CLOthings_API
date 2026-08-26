// 手動照 EF Core Power Tools 的風格寫的，之後重新 Scaffold 資料庫會自動長出類似版本。
// 沒有在 User.cs 加反向的集合屬性（例如 User.Notification），理由跟 ChatMessage.cs 一樣：
// User.cs 是隊友負責的 Users 領域檔案，Sender／Receiver／FromUser 這類指到 User 的關聯，
// 都用單向設定（Controller 裡 .WithMany() 不帶參數），一樣能正常運作，不用去動那個檔案。
#nullable disable
using System;

namespace CLOthings_API.Models;

public partial class Notification
{
    public int NotificationId { get; set; }

    public int UserId { get; set; }

    public int FromUserId { get; set; }

    public string Type { get; set; }

    public int? CommunityPostId { get; set; }

    public DateTimeOffset CreatedDate { get; set; }

    public bool IsRead { get; set; }

    public virtual User User { get; set; }

    public virtual User FromUser { get; set; }

    public virtual CommunityPost CommunityPost { get; set; }
}