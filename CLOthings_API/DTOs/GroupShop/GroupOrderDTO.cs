namespace CLOthings_API.DTOs.GroupShop
{
    // 結帳時前端要傳的收件資訊（對應 GroupCheckoutView 的 orderInfo）
    // UserId 不再由前端傳入，後端一律從 JWT 取得
    public class GroupCheckoutDTO
    {
        public string ShipName { get; set; }

        public string ShipPhone { get; set; }

        public string ShipAddress { get; set; }

        // "宅配到府" / "超商取貨"
        public string PickupMethod { get; set; }

        // "信用卡付款" / "線上支付" / "貨到付款"
        public string PaymentMethod { get; set; }
    }

    // 訂單裡的單一品項（結帳時存進 GroupOrderDetail、之後編輯訂單也會用到）
    public class GroupOrderItemDTO
    {
        public int GroupProductId { get; set; }

        public string ProductName { get; set; }

        public int Quantity { get; set; }

        public int UnitPrice { get; set; }
    }

    // 給「我的團購訂單」列表頁用，對應 GroupOrdersView 的 myOrders 每一筆
    public class GroupOrderListDTO
    {
        public int GroupOrderId { get; set; }

        // 這筆訂單包含的商品名稱，格式跟前端一致："商品A x2、商品B x1"
        public string ProductName { get; set; }

        public string Status { get; set; }

        public int TotalPrice { get; set; }

        public string OrderDate { get; set; }

        public string ShipName { get; set; }
    }

    // 訂單詳細內容，給「編輯訂單」Modal 用
    public class GroupOrderDetailFullDTO : GroupOrderListDTO
    {
        public string ShipPhone { get; set; }

        public string ShipAddress { get; set; }

        public string PickupMethod { get; set; }

        public int Freight { get; set; }

        public List<GroupOrderItemDTO> Items { get; set; } = new();
    }

    // 編輯訂單時前端要傳的資料（對應 GroupOrdersView 的 editForm）
    public class EditGroupOrderDTO
    {
        public string ShipName { get; set; }

        public List<EditGroupOrderItemDTO> Items { get; set; } = new();
    }

    public class EditGroupOrderItemDTO
    {
        public int GroupProductId { get; set; }

        public int Quantity { get; set; }
    }
}
