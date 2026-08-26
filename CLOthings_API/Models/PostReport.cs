// 手動照 EF Core Power Tools 的風格寫的，之後重新 Scaffold 資料庫會自動長出類似版本。
#nullable disable
using System;

namespace CLOthings_API.Models;

public partial class PostReport
{
    public int PostReportId { get; set; }

    public int CommunityPostId { get; set; }

    public int ReporterId { get; set; }

    public string Reason { get; set; }

    public DateTimeOffset CreatedDate { get; set; }

    public virtual CommunityPost CommunityPost { get; set; }

    public virtual User Reporter { get; set; }
}