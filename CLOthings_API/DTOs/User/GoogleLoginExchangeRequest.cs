using System.ComponentModel.DataAnnotations;

namespace CLOthings_API.DTO.User
{
    public class GoogleLoginExchangeRequest
    {
        [Required(ErrorMessage = "登入 Ticket 不能為空")]
        public string Ticket { get; set; } = string.Empty;
    }
}