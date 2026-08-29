using System.ComponentModel.DataAnnotations;

namespace CLOthings_API.DTOs
{
    public class UserAddressDTO
    {
        [Required(ErrorMessage = "請輸入收件人姓名")]
        [StringLength(
            50,
            ErrorMessage = "收件人姓名最多 50 個字"
        )]
        public string RecipientName { get; set; } = string.Empty;


        [Required(ErrorMessage = "請輸入收件人電話")]
        [RegularExpression(
            @"^09\d{8}$",
            ErrorMessage = "手機號碼格式錯誤")]
        public string RecipientPhone { get; set; } = string.Empty;


        [Required(ErrorMessage = "請輸入郵遞區號")]
        [RegularExpression(
            @"^\d{3,6}$",
            ErrorMessage = "郵遞區號格式錯誤")]
        public string PostalCode { get; set; } = string.Empty;


        [Required(ErrorMessage = "請輸入完整地址")]
        [StringLength(
            200,
            ErrorMessage = "地址最多 200 個字")]
        public string AddressDetail { get; set; } = string.Empty;


        public bool? IsDefault { get; set; }
    }
}