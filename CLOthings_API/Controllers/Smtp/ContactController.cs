using Microsoft.AspNetCore.Mvc;

namespace CLOthings_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContactController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        public ContactController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // GET api/contact/email
        [HttpGet("email")]
        public IActionResult GetEmail()
        {
            var email = _configuration["Smtp:Email"];
            if (string.IsNullOrEmpty(email))
            {
                return NotFound("Smtp:Email 尚未設定");
            }
            return Ok(new { email });
        }
    }
}
