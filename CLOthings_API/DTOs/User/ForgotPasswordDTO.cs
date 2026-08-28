using System.ComponentModel.DataAnnotations;

namespace CLOthings_API.DTO.User
{
    public class ForgotPasswordDTO
    {
        [Required(ErrorMessage = "請輸入電子郵件")]
        [EmailAddress(
            ErrorMessage = "Email 格式錯誤，請輸入有效的郵件地址"
        )]
        public string Email { get; set; } = string.Empty;
    }
}