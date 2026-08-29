using System.ComponentModel.DataAnnotations;

namespace CLOthings_API.DTOs
{
    public class UserProfileDTO
    {
        [StringLength(50, ErrorMessage = "名字最多 50 個字")]
        public string? FirstName { get; set; }

        [StringLength(50, ErrorMessage = "姓氏最多 50 個字")]
        public string? LastName { get; set; }

        public string? Avatar { get; set; }

        [RegularExpression(
            @"^(Male|Female)$",
            ErrorMessage = "性別格式錯誤"
        )]
        public string? Gender { get; set; }

        public DateOnly? Birthday { get; set; }

        [StringLength(50, ErrorMessage = "風格標籤最多 50 個字")]
        public string? StyleTag { get; set; }

        [StringLength(500, ErrorMessage = "自我介紹最多 500 個字")]
        public string? Intro { get; set; }
    }
}