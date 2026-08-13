namespace CLOthings_API.DTOs
{
    public class UserDTO
    {
        public int UserId { get; set; }

        public string Username { get; set; }

        public string Account { get; set; }

        public string Password { get; set; }

        public string Email { get; set; }

        public string Phone { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public int UserType { get; set; }

        public int Status { get; set; }

        public bool? TwoFactorEnabled { get; set; }

        public string TwoFactorSecret { get; set; }

        public string CountryCode { get; set; }
    }
}