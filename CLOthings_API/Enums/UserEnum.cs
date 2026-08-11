using System.ComponentModel.DataAnnotations;

namespace CLOthings.Enums
{
    public enum UserTypeEnum
    {
        [Display(Name = "最高管理員")]
        SuperAdmin = 99,   // 最高管理員
        [Display(Name = "一般管理員")]
        Admin = 2,        // 一般管理員
        [Display(Name = "一般用戶")]
        User = 3,         // 一般用戶
        [Display(Name = "封禁用戶")]
        Banned = 4        // 封禁用戶
    }
    public enum StatusEnum
    {
        [Display(Name = "停用")]
        Inactive = 0,
        [Display(Name = "啟用")]
        Active = 1,
    }
    public enum GenderEnum
    {
        [Display(Name = "男")]
        Male,
        [Display(Name = "女")]
        Female
    }
}
