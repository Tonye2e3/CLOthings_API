// 手動照 EF Core Power Tools 的風格寫的，之後重新 Scaffold 資料庫會自動長出類似版本。
#nullable disable
using System;

namespace CLOthings_API.Models;

public partial class ChatMessageImage
{
    public int ChatMessageImageId { get; set; }

    public int ChatMessageId { get; set; }

    public string ImagePath { get; set; }

    public int SortOrder { get; set; }

    public virtual ChatMessage ChatMessage { get; set; }
}