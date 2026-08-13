namespace CLOthings_API.Models;

// 只給「別人看得到的公開資訊」用，不是完整的會員資料，
// 跟隊友的 UserProfileDTO（/me 那支用的，只能查自己）是不同東西。
public class PublicUserProfileDTO
{
    public int UserId { get; set; }
    public string Username { get; set; }
    public string Account { get; set; }
    public string? Avatar { get; set; }
    public string? StyleTag { get; set; }
    public string? Intro { get; set; }
}