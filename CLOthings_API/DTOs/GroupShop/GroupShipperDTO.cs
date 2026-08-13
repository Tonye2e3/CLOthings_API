namespace CLOthings_API.DTOs.GroupShop
{
    public class GroupShipperDTO
    {
        public int GroupShipperId { get; set; }

        public string ShipperName { get; set; }

        public string Email { get; set; }

        public string Address { get; set; }
    }

    public class SaveGroupShipperDTO
    {
        public string ShipperName { get; set; }

        public string Email { get; set; }

        public string Address { get; set; }
    }
}
