namespace CLOthings_API.DTOs.GroupShop
{
    // 客服紀錄，給買家自己查詢或管理端查詢用
    public class GroupCustomerServiceDTO
    {
        public int GroupCustomerServiceId { get; set; }

        public int GroupOrderId { get; set; }

        public string Name { get; set; }

        public string Email { get; set; }

        public string Phone { get; set; }

        public string Title { get; set; }

        public string Content { get; set; }
    }

    // 買家在訂單頁提問時要傳的資料
    public class CreateGroupCustomerServiceDTO
    {
        public string Name { get; set; }

        public string Email { get; set; }

        public string Phone { get; set; }

        public string Title { get; set; }

        public string Content { get; set; }
    }
}
