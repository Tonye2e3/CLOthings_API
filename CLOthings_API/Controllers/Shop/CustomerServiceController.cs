using CLOthings_API.DTOs.Shop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CLOthings_API.Controllers.Shop
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerServiceController : ControllerBase
    {
        private readonly CLOthingsContext _context;
        public CustomerServiceController(CLOthingsContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateCustomerServiceDto dto)
        {
            var cs = new CustomerService
            {
                Name = dto.Name,
                Email = dto.Email,
                Phone = dto.Phone,
                Title = dto.Title,
                Content = dto.Content,
                // OrderId 不填（可空）
            };
            _context.CustomerService.Add(cs);
            await _context.SaveChangesAsync();
            return Ok(new { message = "已收到您的訊息，我們會盡快回覆" });
        }
    }
}