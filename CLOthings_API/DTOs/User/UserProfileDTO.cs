namespace CLOthings_API.DTOs
{
    public class UserProfileDTO
    {
        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public string? Avatar { get; set; }

        public string? Gender { get; set; }

        public DateOnly? Birthday { get; set; }

        public string? StyleTag { get; set; }

        public string? Intro { get; set; }
    }
}