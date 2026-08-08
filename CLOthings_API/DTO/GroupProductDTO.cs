namespace CLOthings_API.DTOs.GroupShop
{
    // 團購階層：對應 GroupDiscountStandard 這張表
    // 前端 tiers 陣列長這樣：{ qty, discount }，這裡照同樣的結構回傳，前端幾乎不用改邏輯
    public class GroupProductTierDTO
    {
        public int Qty { get; set; }

        // discount 是「折扣比例」(0~1)，例如 0.9 代表 9 折；用來讓前端算 listPrice * discount
        public decimal Discount { get; set; }

        // 這個階層算出來的實際團購單價（後端先算好，前端不用重算一次）
        public int UnitPrice { get; set; }
    }

    // 給「商品列表頁 / 商品詳情頁」用的商品資料
    public class GroupProductDTO
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string ImageUrl { get; set; }

        // 原價
        public int ListPrice { get; set; }

        // 商品介紹（GroupProduct.Description）
        public string Intro { get; set; }

        public string Status { get; set; }

        // 目前「已成立訂單」累計的件數（不含尚未結帳的購物車）
        // = 該商品在所有「非已取消」訂單裡的 Quantity 加總
        public int OrderedQty { get; set; }

        public List<GroupProductTierDTO> Tiers { get; set; } = new();
    }
}
