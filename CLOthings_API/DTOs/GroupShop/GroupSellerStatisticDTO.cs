namespace CLOthings_API.DTOs.GroupShop
{
    // 單一商品、單一天的統計數字（對應資料表一筆列）
    public class GroupSellerStatisticDTO
    {
        public int GroupProductId { get; set; }

        public string ProductName { get; set; }

        public int AddCartCount { get; set; }

        public int CheckoutCount { get; set; }

        public int ViewCount { get; set; }

        public int FavorCount { get; set; }

        public string StatisticDate { get; set; }
    }

    // 單一商品的加總數字（所有日期加起來），給總覽表格用
    public class GroupSellerStatisticSummaryDTO
    {
        public int GroupProductId { get; set; }

        public string ProductName { get; set; }

        public int TotalAddCartCount { get; set; }

        public int TotalCheckoutCount { get; set; }

        public int TotalViewCount { get; set; }

        public int TotalFavorCount { get; set; }

        // 加入購物車後，實際結帳的比例（轉換率），用小數表示，例如 0.25 代表 25%
        public double ConversionRate { get; set; }
    }
}
