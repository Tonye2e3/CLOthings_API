namespace CLOthings_API.DTOs.Shop
{
    public class ProductDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string ProductImgFile { get; set; }
        public int ProductCategoryId { get; set; }
    }
}
