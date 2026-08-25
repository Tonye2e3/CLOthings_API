using CLOthings_API.DTOs.Shop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

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

        // POST api/review —— 發表評價
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateReview(CreateReviewDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // 驗證1：這筆訂單明細，是這個使用者買的、而且訂單已完成
            var orderDetail = await _context.OrderDetail
                .Include(d => d.Order)
                .FirstOrDefaultAsync(d => d.OrderDetailId == dto.OrderDetailId);

            if (orderDetail == null || orderDetail.Order.UserId != userId)
            {
                return BadRequest(new { message = "找不到這筆訂單明細，或不是你的訂單" });
            }

            if (orderDetail.Order.Status != "已完成")
            {
                return BadRequest(new { message = "訂單完成後才能評價" });
            }

            // 驗證2：這筆明細評過了嗎？
            var exists = await _context.Review.AnyAsync(r => r.OrderDetailId == dto.OrderDetailId);
            if (exists)
            {
                return BadRequest(new { message = "這筆商品已經評價過了" });
            }

            // 驗證3：評分範圍
            if (dto.Rating < 1 || dto.Rating > 5)
            {
                return BadRequest(new { message = "評分必須是 1 到 5" });
            }

            var review = new Review
            {
                UserId = userId,
                OrderDetailId = dto.OrderDetailId,
                Rating = dto.Rating,
                ReviewComment = dto.ReviewComment,
                ReviewDatetime = DateTimeOffset.UtcNow,
            };
            _context.Review.Add(review);
            await _context.SaveChangesAsync();

            return Ok(new { message = "評價成功" });
        }



    }
}