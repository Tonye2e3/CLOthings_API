namespace CLOthings_API.DTOs.GroupShop
{
    // 給管理端「所有訂單」列表用，比買家端多帶 UserId / 物流資訊
    public class GroupOrderAdminListDTO
    {
        public int GroupOrderId { get; set; }

        public int UserId { get; set; }

        public string ProductName { get; set; }

        public string Status { get; set; }

        public int TotalPrice { get; set; }

        public string OrderDate { get; set; }

        public string PickupMethod { get; set; }

        public string ShipName { get; set; }

        public string ShipPhone { get; set; }

        public string ShipAddress { get; set; }

        public int? GroupShipperId { get; set; }

        public string ShipperName { get; set; }

        public string ShipperDate { get; set; }
    }

    // 管理端更新訂單狀態
    public class UpdateOrderStatusDTO
    {
        // 例如 "進行中 (組團中)" / "已成團 (備貨中)" / "已完成" / "已取消"
        public string Status { get; set; }
    }

    // 管理端指派物流
    public class AssignShipperDTO
    {
        public int GroupShipperId { get; set; }

        public DateTimeOffset? ShipperDate { get; set; }
    }
}
