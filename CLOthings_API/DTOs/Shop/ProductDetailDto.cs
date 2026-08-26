namespace CLOthings_API.DTOs.Shop
{
    public class ProductDetailDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; }

        // 規格清單（一個商品有多個規格）← 巢狀！
        public List<ProductDetailSpecDto> Specifications { get; set; }

        // 圖片清單（一個商品有多張圖）
        public List<string> Images { get; set; }
    }
}
