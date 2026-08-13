using CLOthings_API.DTOs.Shop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CLOthings_API.Controllers.Shop
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] //整個Controller都需要登入才能用  
    public class CartController : ControllerBase
    {
        private readonly CLOthingsContext _context;

        public CartController(CLOthingsContext context)
        {
            _context = context;
        }

        // GET api/cart —— 拿「我的」購物車
        [HttpGet]
        public async Task<IActionResult> GetMyCart()
        {
            // ① 從 token 讀出當前使用者的 UserId
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // ② 撈這個使用者的購物車，順著關聯撈出商品資訊
            var cartItems = await _context.Cart
                .Where(c => c.UserId == userId)                    // 只要「我的」
                .Include(c => c.ProductSpecification)              // 撈規格
                    .ThenInclude(s => s.Product)                   // 從規格撈商品
                        .ThenInclude(p => p.ProductImg)            // 從商品撈圖片
                .Select(c => new CartItemDto
                {
                    CartId = c.CartId,
                    ProductSpecificationId = c.ProductSpecificationId,
                    ProductId = c.ProductSpecification.ProductId,
                    ProductName = c.ProductSpecification.Product.ProductName,
                    Price = c.ProductSpecification.Product.Price,
                    Color = c.ProductSpecification.Color,
                    Size = c.ProductSpecification.Size,
                    Image = c.ProductSpecification.Product.ProductImg
                                .Select(img => img.ProductImgFile).FirstOrDefault(),
                    Quantity = c.Quantity,
                })
                .ToListAsync();

            return Ok(cartItems);
        }
    }
}
