namespace CLOthings_API.DTOs.GroupShop
{
    // 建立付款請求後回傳給前端：帶著 paymentId 讓前端導去「模擬付款頁」
    public class CreatePaymentResultDTO
    {
        public string PaymentId { get; set; }

        public int Amount { get; set; }
    }

    // 「模擬付款頁」打開時，用來顯示金額等資訊
    public class PendingPaymentDTO
    {
        public string PaymentId { get; set; }

        public int Amount { get; set; }

        public string PaymentMethod { get; set; }

        public List<GroupOrderItemDTO> Items { get; set; } = new();
    }

    // 在「模擬付款頁」按下「付款成功」或「付款失敗」時，前端要傳的資料
    public class ConfirmPaymentDTO
    {
        public bool Success { get; set; }
    }

    // 呼叫 LINE Pay Request API 成功後，回傳給前端的付款頁網址
    public class LinePayRequestResultDTO
    {
        public string PaymentUrl { get; set; }
    }
}
