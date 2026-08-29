using System.ComponentModel.DataAnnotations;

namespace CLOthings_API.DTOs
{
    public class RegisterDTO
    {
        // 🟢 使用者名稱
        [Required(ErrorMessage = "請輸入使用者名稱")]
        [StringLength(
            50,
            ErrorMessage = "使用者名稱最多 50 個字元"
        )]
        public string Username { get; set; } = string.Empty;


        // 🟢 帳號：只能英數字，4～50 字元
        [Required(ErrorMessage = "請輸入帳號")]
        [RegularExpression(
            @"^[a-zA-Z0-9]{4,50}$",
            ErrorMessage = "帳號格式錯誤，只能英數字，4～50 字元"
        )]
        public string Account { get; set; } = string.Empty;


        // 🟢 Email
        [Required(ErrorMessage = "請輸入電子郵件")]
        [EmailAddress(
            ErrorMessage = "Email 格式錯誤，請輸入有效的郵件地址"
        )]
        public string Email { get; set; } = string.Empty;


        // 🟢 密碼：至少一個大寫、一個小寫，只能英數字，至少 6 字元
        [Required(ErrorMessage = "請輸入密碼")]
        [RegularExpression(
            @"^(?=.*[A-Z])(?=.*[a-z])[A-Za-z0-9]{6,}$",
            ErrorMessage = "密碼格式錯誤，需包含至少一個大寫與一個小寫字母，只能英數字，至少 6 字元"
        )]
        public string Password { get; set; } = string.Empty;


        // 手機：必填，09 開頭，共 10 碼
        [Required(ErrorMessage = "請輸入手機號碼")]
        [RegularExpression(
            @"^09\d{8}$",
            ErrorMessage = "請填入正確的手機格式（需 09 開頭，共 10 碼）"
        )]
        public string Phone { get; set; } = string.Empty;
    }
}