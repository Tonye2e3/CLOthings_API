namespace CLOthings_API.Models;

public class CommentDTO
{
    public int PostCommentId { get; set; }
    public int? ParentCommentId { get; set; } // 回覆別人的留言時才會有值，直接留言在貼文底下時是 null
    public int CommunityPostId { get; set; }
    public int UserId { get; set; }
    public string? User { get; set; }   // 留言者名稱，查詢時組出來的唯讀資訊，新增留言時不用填
    public string? Avatar { get; set; } // 留言者大頭貼，查詢時組出來的唯讀資訊，新增留言時不用填
    public string CommentText { get; set; }
    public DateTimeOffset CommentDate { get; set; }
}