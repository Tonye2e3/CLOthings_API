namespace CLOthings_API.Models;

public class LikeDTO
{
    public int PostLikesId { get; set; }
    public int CommunityPostId { get; set; }
    public int UserId { get; set; }
    public DateTimeOffset LikeDate { get; set; }
}