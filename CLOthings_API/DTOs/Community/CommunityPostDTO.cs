namespace CLOthings_API.Models;

public class CommunityPostDTO
{
    public int CommunityPostId { get; set; }
    public int UserId { get; set; }
    public string Content { get; set; }
    public DateTimeOffset PostDate { get; set; }
    public string Status { get; set; }

    public UserSummaryDTO User { get; set; }
    public List<PostImageDTO> Images { get; set; }
    public int LikesCount { get; set; }
    public int CommentsCount { get; set; }
    public List<TaggedProductDTO> TaggedProducts { get; set; }
}

public class UserSummaryDTO
{
    public int UserId { get; set; }
    public string Name { get; set; }
    public string Avatar { get; set; }
}

public class PostImageDTO
{
    public int PostImageId { get; set; }
    public string ImageFileName { get; set; }
    public int SortOrder { get; set; }
}

public class TaggedProductDTO
{
    public int PostTaggedProductId { get; set; }
    public int ProductId { get; set; }
    public string ProductRoute { get; set; }
    public string Name { get; set; }
}