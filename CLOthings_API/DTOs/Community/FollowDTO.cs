namespace CLOthings_API.Models;

public class FollowDTO
{
    public int UserFollowId { get; set; }
    public int FollowerId { get; set; }
    public int FollowingId { get; set; }
}

public class FollowCountsDTO
{
    public int FollowersCount { get; set; }
    public int FollowingCount { get; set; }
}