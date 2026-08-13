namespace CLOthings_API.DTOs.GroupShop
{
    // ---- 分類 ----
    public class GroupProductCategoryDTO
    {
        public int GroupProductCategoryId { get; set; }

        public string CategoryName { get; set; }

        public string Description { get; set; }
    }

    public class SaveGroupProductCategoryDTO
    {
        public string CategoryName { get; set; }

        public string Description { get; set; }
    }

    // ---- 供應商 ----
    public class GroupSupplierDTO
    {
        public int GroupSupplierId { get; set; }

        public string SupplierName { get; set; }

        public string ContactName { get; set; }

        public string ContactTitle { get; set; }

        public string Address { get; set; }

        public string Phone { get; set; }
    }

    public class SaveGroupSupplierDTO
    {
        public string SupplierName { get; set; }

        public string ContactName { get; set; }

        public string ContactTitle { get; set; }

        public string Address { get; set; }

        public string Phone { get; set; }
    }
}
