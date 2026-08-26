namespace CLOthings_API.Models;

// PostReportDTO：前端呼叫「檢舉」時送的資料，也是後端回傳給前端的格式。
public class PostReportDTO
{
    public int PostReportId { get; set; }
    public int CommunityPostId { get; set; }
    public int ReporterId { get; set; }
    public string Reason { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
}

// PostReportSummaryDTO：後台列表要用的「這篇貼文一共被檢舉幾次」統計資料。
public class PostReportSummaryDTO
{
    public int CommunityPostId { get; set; }
    public int ReportCount { get; set; }
}

// BlockDTO：前端呼叫「封鎖」時送的資料。
public class BlockDTO
{
    public int UserBlockId { get; set; }
    public int BlockerId { get; set; }
    public int BlockedId { get; set; }
}