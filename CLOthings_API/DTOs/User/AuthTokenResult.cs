namespace CLOthings_API.DTOs.User
{

    // =============================================
    // Token Service 回傳資料
    // =============================================
    public class AuthTokenResult
    {
        public string AccessToken { get; set; } = null!;

        public string RefreshToken { get; set; } = null!;

        public DateTimeOffset RefreshTokenExpiresAt
        {
            get;
            set;
        }

        public int UserId { get; set; }

        public string Name { get; set; } = null!;

        public string Account { get; set; } = null!;

        public string Role { get; set; } = null!;

    }
}