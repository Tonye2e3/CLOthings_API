namespace CLOthings_API.DTOs.GroupShop
{
    // ---- 商品：新增/編輯 ----
    public class SaveGroupProductDTO
    {
        public string ProductName { get; set; }

        public int GroupSupplierId { get; set; }

        public int GroupProductCategoryId { get; set; }

        public string Description { get; set; }

        public int Price { get; set; }

        // "上架中" / "已下架" 之類，前端下拉選單自訂
        public string Status { get; set; } = "上架中";

        public string ProductImg { get; set; }

        public DateTimeOffset? SalesStart { get; set; }

        public DateTimeOffset? SalesEnd { get; set; }
    }

    // ---- 團購階層：新增/編輯 ----
    public class TierAdminDTO
    {
        public int GroupDiscountStandardId { get; set; }

        public string TierLevel { get; set; }

        public int ThresholdCount { get; set; }

        public decimal DiscountRate { get; set; }
    }

    public class SaveTierDTO
    {
        // 例如 "第一階" / "第二階"
        public string TierLevel { get; set; }

        public int ThresholdCount { get; set; }

        // 折扣比例 0~1，例如 0.9 代表 9 折
        public decimal DiscountRate { get; set; }
    }

    // ---- 商品規格（尺寸/顏色）：新增 ----
    public class SpecAdminDTO
    {
        public int GroupProductSpecificationId { get; set; }

        public string Size { get; set; }

        public string Color { get; set; }
    }

    public class SaveSpecDTO
    {
        public string Size { get; set; } = "Free";

        public string Color { get; set; } = "Free";
    }
}
