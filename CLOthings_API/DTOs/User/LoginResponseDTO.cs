namespace CLOthings_API.DTO.User
{
    public class LoginResponseDTO
    {
        public string Token { get; set; }
        public UserDto User { get; set; }
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string Account { get; set; }
        public string Name { get; set; }
        public string Role { get; set; } // "admin" 或 "member"
    }
}
