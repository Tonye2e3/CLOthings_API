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

        // POST api/cart —— 加入購物車
        [HttpPost]
        public async Task<IActionResult> AddToCart(AddCartDto dto)
        {
            // 從 token 讀當前使用者
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // ① 檢查：這個使用者的購物車，已經有這個規格了嗎？（upsert 的判斷）
            var existItem = await _context.Cart
                .FirstOrDefaultAsync(c =>
                    c.UserId == userId &&
                    c.ProductSpecificationId == dto.ProductSpecificationId);

            if (existItem != null)
            {
                // ② 已存在 → 數量疊加
                existItem.Quantity += dto.Quantity;
            }
            else
            {
                // ③ 不存在 → 新增一筆
                var newItem = new Cart
                {
                    UserId = userId,
                    ProductSpecificationId = dto.ProductSpecificationId,
                    Quantity = dto.Quantity,
                };
                _context.Cart.Add(newItem);
            }

            // ④ 存進資料庫
            await _context.SaveChangesAsync();

            return Ok(new { message = "已加入購物車" });
        }

        // PUT api/cart/5 —— 改某筆購物車項目的數量
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateQuantity(int id, UpdateCartDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // 找出這筆購物車項目（而且必須是「這個使用者的」）
            var cartItem = await _context.Cart
                .FirstOrDefaultAsync(c => c.CartId == id && c.UserId == userId);

            if (cartItem == null)
            {
                return NotFound(new { message = "找不到這筆購物車項目" });
            }

            // 更新數量
            cartItem.Quantity = dto.Quantity;
            await _context.SaveChangesAsync();

            return Ok(new { message = "數量已更新" });
        }

        // DELETE api/cart/5 —— 移除某筆購物車項目
        [HttpDelete("{id}")]
        public async Task<IActionResult> RemoveFromCart(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // 找出這筆（同樣要確認是「這個使用者的」）
            var cartItem = await _context.Cart
                .FirstOrDefaultAsync(c => c.CartId == id && c.UserId == userId);

            if (cartItem == null)
            {
                return NotFound(new { message = "找不到這筆購物車項目" });
            }

            // 刪除
            _context.Cart.Remove(cartItem);
            await _context.SaveChangesAsync();

            return Ok(new { message = "已移除" });
        }



    }
}
