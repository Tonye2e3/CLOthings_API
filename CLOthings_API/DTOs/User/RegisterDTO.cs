namespace CLOthings_API.DTOs
{
    public class RegisterDTO
    {
        public string Username { get; set; } = string.Empty;

        public string Account { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string? Phone { get; set; }
    }
}