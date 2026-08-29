using System.ComponentModel.DataAnnotations;

namespace CLOthings_API.DTO.User
{
    public class SetPasswordDTO
    {
        [Required(ErrorMessage = "請輸入新密碼")]
        [RegularExpression(
            @"^(?=.*[A-Z])(?=.*[a-z])[A-Za-z0-9]{6,}$",
            ErrorMessage = "密碼格式錯誤，需包含至少一個大寫與一個小寫字母，只能英數字，至少 6 字元"
        )]
        public string NewPassword { get; set; } = string.Empty;
    }
}