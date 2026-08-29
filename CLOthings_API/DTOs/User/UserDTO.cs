namespace CLOthings_API.DTO.User
{
    public class UserDTO
    {
        public int UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string Account { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;
    }
}