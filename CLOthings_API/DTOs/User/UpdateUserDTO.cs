using System.ComponentModel.DataAnnotations;

namespace CLOthings_API.DTOs
{
    public class UpdateUserDTO
    {
        // 使用者名稱：必填，最多 50 字元
        [Required(ErrorMessage = "請輸入使用者名稱")]
        [StringLength(
            50,
            ErrorMessage = "使用者名稱最多 50 個字元"
        )]
        public string Username { get; set; } = string.Empty;


        // Email：必填，且必須符合 Email 格式
        [Required(ErrorMessage = "請輸入電子郵件")]
        [EmailAddress(
            ErrorMessage = "Email 格式錯誤，請輸入有效的郵件地址"
        )]
        public string Email { get; set; } = string.Empty;


        // 手機：必填，09 開頭，共 10 碼
        [Required(ErrorMessage = "請輸入手機號碼")]
        [RegularExpression(
            @"^09\d{8}$",
            ErrorMessage = "請填入正確的手機格式（需 09 開頭，共 10 碼）"
        )]
        public string Phone { get; set; } = string.Empty;


        // 國碼：目前選填
        public string? CountryCode { get; set; }
    }
}