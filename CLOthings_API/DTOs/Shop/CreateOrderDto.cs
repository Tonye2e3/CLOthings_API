namespace CLOthings_API.DTOs.Shop
{
    public class CreateOrderDto
    {
        public string ShipName { get; set; }      // 收件人
        public string ShipAddress { get; set; }   // 地址
        public string ShipPhone { get; set; }     // 電話
        public List<OrderDetailDto> Items { get; set; }  // 要結帳的商品清單
    }
    public class OrderDetailDto {
        public int ProductSpecificationId { get; set; }
        public int Quantity { get; set; }
        // 價格後端查，避免有狗自己改價格
    }
}
