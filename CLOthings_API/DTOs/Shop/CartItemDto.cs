namespace CLOthings_API.DTOs.Shop
{
    public class CartItemDto
    {
        public int CartId { get; set; }                    // 購物車項目 id
        public int ProductSpecificationId { get; set; }    // 規格 id（前端辨識用）
        public int ProductId { get; set; }                 // 商品 id
        public string ProductName { get; set; }            // 商品名
        public decimal Price { get; set; }                 // 單價
        public string Color { get; set; }                  // 顏色
        public string Size { get; set; }                   // 尺寸
        public string Image { get; set; }                  // 圖片檔名
        public int Quantity { get; set; }                  // 數量
    }
}
