using System.ComponentModel.DataAnnotations;

namespace CLOthings_API.DTO.User
{
    public class EnableTwoFactorDTO
    {
        [Required(ErrorMessage = "請輸入驗證碼")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "驗證碼必須是 6 位數字")]
        public string Code { get; set; } = string.Empty;
    }
}