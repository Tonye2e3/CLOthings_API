namespace CLOthings_API.DTOs.Shop
{
    public class OrderDetailViewDto
    {
        public int OrderId { get; set; }
        public DateTimeOffset OrderDate { get; set; }
        public string Status { get; set; }
        public string ShipName { get; set; }
        public string ShipAddress { get; set; }
        public string ShipPhone { get; set; }
        public decimal Total { get; set; }
        public List<OrderItemViewDto> Items { get; set; }   // 明細清單（巢狀）
    }
    // 訂單裡的每個商品明細
    public class OrderItemViewDto
    {
        public string ProductName { get; set; }
        public string Color { get; set; }
        public string Size { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}
