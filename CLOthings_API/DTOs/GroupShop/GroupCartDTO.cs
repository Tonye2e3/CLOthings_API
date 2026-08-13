namespace CLOthings_API.DTOs.GroupShop
{
    // 購物車裡的一筆商品，給「購物車頁 / 結帳頁」顯示用
    public class GroupCartItemDTO
    {
        public int GroupCartId { get; set; }

        public int GroupProductId { get; set; }

        public int GroupProductSpecificationId { get; set; }

        public string Name { get; set; }

        public string ImageUrl { get; set; }

        public int ListPrice { get; set; }

        // 目前應該用的單價：已解鎖團購價就用團購價，沒解鎖就用原價
        public int UnitPrice { get; set; }

        // 是否已解鎖團購價（給前端顯示「已解鎖」標籤用）
        public bool Unlocked { get; set; }

        public int Quantity { get; set; }
    }

    // 加入購物車時前端要傳的資料
    // UserId 不再由前端傳入，後端一律從 JWT 取得，避免有人改 UserId 就能操作別人的購物車
    public class AddGroupCartDTO
    {
        public int GroupProductId { get; set; }

        // 選填：沒有指定的話，後端會自動挑該商品的第一個規格（目前前端還沒有尺寸/顏色選擇 UI）
        public int? GroupProductSpecificationId { get; set; }

        public int Quantity { get; set; } = 1;
    }

    // 修改購物車某一項數量時要傳的資料
    public class UpdateGroupCartDTO
    {
        public int Quantity { get; set; }
    }
}
