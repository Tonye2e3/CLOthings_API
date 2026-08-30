using System.ComponentModel.DataAnnotations;

namespace CLOthings_API.DTO.User
{
    public class VerifyEmailDTO
    {
        [Required(ErrorMessage = "驗證 Token 不可為空")]
        public string Token { get; set; } = string.Empty;
    }
}