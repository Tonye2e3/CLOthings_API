namespace CLOthings_API.DTOs
{
    public class UpdateUserDTO
    {
        public string Username { get; set; }

        public string Email { get; set; }

        public string Phone { get; set; }

        public string? CountryCode { get; set; }
    }
}