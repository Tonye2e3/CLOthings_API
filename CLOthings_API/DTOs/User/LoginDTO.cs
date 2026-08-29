using System.ComponentModel.DataAnnotations;

namespace CLOthings_API.DTO.User
{
    public class LoginDTO
    {
        [Required(ErrorMessage = "請輸入帳號或電子郵件")]
        public string Account { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入密碼")]
        public string Password { get; set; } = string.Empty;

    }
}