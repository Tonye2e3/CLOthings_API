using CLOthings_API.DTOs.Shop;
using CLOthings_API.Models;
using CLOthings_API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CLOthings_API.Services;

namespace CLOthings_API.Controllers.Shop
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerServiceController : ControllerBase
    {
        private readonly CLOthingsContext _context;
        private readonly EmailService _emailService; // SMTP那個寄信服務 
        public CustomerServiceController(CLOthingsContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
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

            // 寄通知信給客服信箱
            try
            {
                var body = $"收到新的客服訊息：\n\n" +
                           $"姓名：{dto.Name}\n" +
                           $"Email：{dto.Email}\n" +
                           $"電話：{dto.Phone}\n" +
                           $"主旨：{dto.Title}\n" +
                           $"內容：{dto.Content}";

                await _emailService.SendAsync("sandy881133@gmail.com", $"[客服訊息] {dto.Title}", body);
            }
            catch (Exception ex)
            {
                // 寄信失敗不影響「訊息已存資料庫」，只記錄
                Console.WriteLine("寄信失敗：" + ex.Message);
            }

            return Ok(new { message = "已收到您的訊息，我們會盡快回覆" });
        }
    }
}