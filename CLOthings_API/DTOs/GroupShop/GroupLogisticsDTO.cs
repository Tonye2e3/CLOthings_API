#nullable disable
namespace CLOthings_API.DTOs.GroupShop
{
    // 電子地圖選完門市後，7-11（統一超商）用 POST 方式回傳的資料
    // 欄位名稱是超商電子地圖系統固定的格式，不是我們自己定的
    // 這幾個欄位不一定每次都會帶（例如 TempVar 可能沒用到就不會回傳），所以不能當必填
    public class CvsMapCallbackDTO
    {
        public string StoreID { get; set; }

        public string StoreName { get; set; }

        public string Address { get; set; }

        public string TempVar { get; set; }
    }
}
