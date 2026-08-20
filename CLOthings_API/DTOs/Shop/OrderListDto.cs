namespace CLOthings_API.DTOs.Shop
{
    public class OrderListDto
    {
        public int OrderId { get; set; }
        public DateTimeOffset OrderDate { get; set; }
        public string Status { get; set; }
        public decimal Total { get; set; }   // 總金額（從明細算出來）
    }
}
