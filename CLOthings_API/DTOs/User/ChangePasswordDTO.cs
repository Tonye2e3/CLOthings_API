namespace CLOthings_API.DTOs
{
    public class ChangePasswordDTO
    {
        // 目前的舊密碼
        public string CurrentPassword { get; set; } = string.Empty;

        // 要設定的新密碼
        public string NewPassword { get; set; } = string.Empty;
    }
}