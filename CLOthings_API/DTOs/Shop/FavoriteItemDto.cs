namespace CLOthings_API.DTOs.Shop
{
    public class FavoriteItemDto
    {
        public int CustomerFavoriteId { get; set; }   // 收藏項目 id（刪除用）
        public int ProductId { get; set; }            // 商品 id（點擊進詳情用）
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public string Image { get; set; }             // 圖片檔名
    }
}
