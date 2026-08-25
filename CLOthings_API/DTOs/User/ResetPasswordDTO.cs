namespace CLOthings_API.DTO.User
{
    public class ResetPasswordDTO
    {
        public string Token { get; set; } = string.Empty;

        public string NewPassword { get; set; } = string.Empty;
    }
}