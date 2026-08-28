using System.ComponentModel.DataAnnotations;

namespace CLOthings_API.DTO.User
{
    public class ResetPasswordDTO
    {
        // Token 必填
        [Required(ErrorMessage = "重設密碼 Token 不能為空")]
        public string Token { get; set; } = string.Empty;


        // 新密碼：至少一個大寫、一個小寫，只能英數字，至少 6 字元
        [Required(ErrorMessage = "請輸入新密碼")]
        [RegularExpression(
            @"^(?=.*[A-Z])(?=.*[a-z])[A-Za-z0-9]{6,}$",
            ErrorMessage = "密碼格式錯誤，需包含至少一個大寫與一個小寫字母，只能英數字，至少 6 字元"
        )]
        public string NewPassword { get; set; } = string.Empty;
    }
}