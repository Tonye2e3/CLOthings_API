using CLOthings_API.DTOs.Shop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CLOthings_API.Controllers.Shop
{
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class FavoriteController : ControllerBase
    {
        private readonly CLOthingsContext _context;

        public FavoriteController(CLOthingsContext context)
        {
            _context = context;
        }

        // GET api/favorite —— 拿收藏
        [HttpGet]
        public async Task<IActionResult> GetFavorite()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var favorites = await _context.CustomerFavorite
                .Where(f => f.UserId == userId)
                .Include(f => f.Product).ThenInclude(p => p.ProductImg)
                .Select(f => new FavoriteItemDto
                {
                    CustomerFavoriteId = f.CustomerFavoriteId,
                    ProductId = f.ProductId,
                    ProductName = f.Product.ProductName,
                    Price = f.Product.Price,
                    Image = f.Product.ProductImg.Select(img => img.ProductImgFile).FirstOrDefault()
                }).ToListAsync();
            return Ok(favorites);
        }

        // POST api/favorite 家收藏
        [HttpPost]
        public async Task<IActionResult> AddFavorite(AddFavoriteDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var existfavorite = await _context.CustomerFavorite
                .FirstOrDefaultAsync(f =>
                    f.UserId == userId &&
                    f.ProductId == dto.ProductId);

            if (existfavorite != null)
            {
                // ② 已存在 
                return Ok(new { message = "已在收藏中" });
            }
            else
            {
                // ③ 不存在 → 新增一筆
                var newFavorite = new CustomerFavorite
                {
                    UserId = userId,
                    ProductId = dto.ProductId,
                };
                _context.CustomerFavorite.Add(newFavorite);
            }

            // ④ 存進資料庫
            await _context.SaveChangesAsync();

            return Ok(new { message = "已加入收藏" });
        }

        // DELETE api/favorite 刪除收藏
        [HttpDelete("{id}")]
        public async Task<IActionResult> RemoveFavorite(int id) {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var favoriteItem = await _context.CustomerFavorite
                .FirstOrDefaultAsync(f => f.CustomerFavoriteId == id && f.UserId == userId);

            if (favoriteItem == null)
            {
                return NotFound(new { message = "找不到這筆收藏" });
            }

            // 刪除
            _context.CustomerFavorite.Remove(favoriteItem);
            await _context.SaveChangesAsync();

            return Ok(new { message = "已移除" });
        }


    

    }

}
