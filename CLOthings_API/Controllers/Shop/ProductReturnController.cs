using CLOthings_API.DTOs.Shop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CLOthings_API.Controllers.Shop
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProductReturnController : ControllerBase
    {
        private readonly CLOthingsContext _context;

        public ProductReturnController(CLOthingsContext context)
        {
            _context = context;
        }

        // POST api/productreturn —— 建立退貨
        [HttpPost]
        public async Task<IActionResult> CreateReturn(CreateReturnDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // ───── 驗證 1：這筆訂單存在，而且是「這個使用者的」─────
            var order = await _context.Order
                .Include(o => o.OrderDetail)   // 撈明細，等下驗證數量用
                .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId && o.UserId == userId);

            if (order == null)
            {
                return BadRequest(new { message = "找不到這筆訂單，或這不是你的訂單" });
            }

            // ───── 驗證 2：有沒有要退的商品 ─────
            if (dto.Items == null || dto.Items.Count == 0)
            {
                return BadRequest(new { message = "沒有選擇要退貨的商品" });
            }

            // ───── 建立退貨主檔 ─────
            var productReturn = new ProductReturn
            {
                OrderId = dto.OrderId,
                UserId = userId,
                Reason = dto.Reason,
                Status = "退貨申請中",
                ReturnDate = DateTimeOffset.UtcNow,
                RefundAmount = 0,   // 先設 0，等下算完再填
            };

            decimal totalRefund = 0;   // 累計退款金額

            // ───── 逐筆處理要退的明細 ─────
            foreach (var item in dto.Items)
            {
                // 驗證 3：這筆退貨明細，對應的 OrderDetail 真的在這筆訂單裡嗎？
                var orderDetail = order.OrderDetail
                    .FirstOrDefault(d => d.OrderDetailId == item.OrderDetailId);

                if (orderDetail == null)
                {
                    return BadRequest(new { message = $"訂單裡沒有這筆明細 {item.OrderDetailId}" });
                }

                // 驗證 4：退的數量，不能超過買的數量 ★你的設計讓這步可行！
                if (item.Quantity <= 0 || item.Quantity > orderDetail.Quantity)
                {
                    return BadRequest(new { message = $"退貨數量不對（買了 {orderDetail.Quantity} 件）" });
                }

                // 建立退貨明細
                var returnDetail = new ProductReturnDetail
                {
                    OrderDetailId = item.OrderDetailId,
                    Quantity = item.Quantity,
                };
                productReturn.ProductReturnDetail.Add(returnDetail);

                // 累加退款金額（退的數量 × 當時單價）
                totalRefund += orderDetail.Price * item.Quantity;
            }

            // ───── 填入算好的退款金額 ─────
            productReturn.RefundAmount = totalRefund;

            // ───── 存進資料庫（主檔 + 明細）─────
            _context.ProductReturn.Add(productReturn);

            // （可選）把訂單狀態改成「退貨申請中」
            order.Status = "退貨申請中";

            await _context.SaveChangesAsync();

            return Ok(new { message = "退貨申請已送出", returnId = productReturn.ReturnId });
        }
    }
}