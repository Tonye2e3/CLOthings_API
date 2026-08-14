namespace CLOthings_API.Models;

public class ShortUrlRequestDTO
{
    public int CommunityPostId { get; set; }
}

public class ShortUrlResponseDTO
{
    public string ShortCode { get; set; }
}