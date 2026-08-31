using System.ComponentModel.DataAnnotations;

namespace CLOthings_API.DTO.User
{
    public class DisableTwoFactorDTO
    {
        [Required(ErrorMessage = "驗證碼不可為空")]
        [RegularExpression(@"^\d{6}$",
            ErrorMessage = "驗證碼必須為 6 位數字")]
        public string Code { get; set; } = string.Empty;
    }
}