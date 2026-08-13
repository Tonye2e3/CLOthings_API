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

public class CreatorDTO
{
    public int UserId { get; set; }
    public string Name { get; set; }
    public string Avatar { get; set; }
    public int FollowersCount { get; set; }
    public bool IsFollowing { get; set; }
    public int? UserFollowId { get; set; } // 有追蹤時才有值，取消追蹤要用它
}