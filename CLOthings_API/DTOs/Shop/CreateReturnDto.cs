namespace CLOthings_API.DTOs.Shop
{
    public class CreateReturnDto
    {
        public int OrderId { get; set; }
        public string Reason { get; set; }
        public List<ReturnItemDto> Items { get; set; }   // 要退的明細
    }

    public class ReturnItemDto
    {
        public int OrderDetailId { get; set; }   // 退訂單的哪筆明細
        public int Quantity { get; set; }         // 退幾件
    }
}