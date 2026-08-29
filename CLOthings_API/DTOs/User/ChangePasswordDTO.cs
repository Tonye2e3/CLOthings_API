using System.ComponentModel.DataAnnotations;

namespace CLOthings_API.DTOs
{
    public class ChangePasswordDTO
    {
        // 目前的舊密碼：只需要必填
        [Required(ErrorMessage = "請輸入目前密碼")]
        public string CurrentPassword { get; set; } = string.Empty;


        // 新密碼：必填 + 密碼格式驗證
        [Required(ErrorMessage = "請輸入新密碼")]
        [RegularExpression(
            @"^(?=.*[A-Z])(?=.*[a-z])[A-Za-z0-9]{6,}$",
            ErrorMessage = "密碼格式錯誤，需包含至少一個大寫與一個小寫字母，只能英數字，至少 6 字元"
        )]
        public string NewPassword { get; set; } = string.Empty;
    }
}