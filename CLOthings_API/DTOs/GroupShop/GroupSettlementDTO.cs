namespace CLOthings_API.DTOs.GroupShop
{
    public class SettledProductDTO
    {
        public int GroupProductId { get; set; }

        public string ProductName { get; set; }

        public int OrderedQty { get; set; }

        public int RequiredQty { get; set; }
    }

    public class RefundNoticeDTO
    {
        public int GroupOrderId { get; set; }

        public string ToEmail { get; set; }

        public string Subject { get; set; }

        public string Body { get; set; }
    }

    public class GroupSettlementResultDTO
    {
        // 這次結算裡，達標成團的商品
        public List<SettledProductDTO> SucceededProducts { get; set; } = new();

        // 這次結算裡，沒達標而流團的商品
        public List<SettledProductDTO> FailedProducts { get; set; } = new();

        // 因為流團被自動取消、需要退款的訂單，以及對應「本來會寄出的信」內容
        public List<RefundNoticeDTO> RefundNotices { get; set; } = new();

        // 因為商品成團而轉為「已成團 (備貨中)」的訂單編號
        public List<int> ConfirmedOrderIds { get; set; } = new();
    }
}
