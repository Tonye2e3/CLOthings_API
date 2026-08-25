using CLOthings_API.DTOs.Shop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CLOthings_API.Controllers.Shop
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewController : ControllerBase
    {
        private readonly CLOthingsContext _context;
        public ReviewController(CLOthingsContext context)
        {
            _context = context;
        }

        // GET api/review/product/{productId} —— 某商品的所有評價
        [HttpGet("product/{productId}")]
        public async Task<IActionResult> GetProductReviews(int productId)
        {
            var reviews = await _context.Review
                // 這筆評價的訂單明細 → 規格 → 商品，是這個 productId
                .Where(r => r.OrderDetail.ProductSpecification.ProductId == productId)
                .Include(r => r.User)
                .OrderByDescending(r => r.ReviewDatetime)   // 新的在前
                .Select(r => new ReviewDto
                {
                    ReviewId = r.ReviewId,
                    Rating = r.Rating,
                    ReviewComment = r.ReviewComment,
                    ReviewDatetime = r.ReviewDatetime,
                    ReviewImg = r.ReviewImg,
                    UserName = r.User.Username,   // 確認你 User 的名字欄位叫什麼
                })
                .ToListAsync();

            return Ok(reviews);
        }
    }
}