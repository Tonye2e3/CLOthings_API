using CLOthings_API.DTOs.Shop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.NetworkInformation;
using System.Security.Claims;


namespace CLOthings_API.Controllers.Shop
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly CLOthingsContext _context;

        public OrderController(CLOthingsContext context)
        {
            _context = context;
        }

        // POST api/order —— 建立訂單
        [HttpPost]
        public async Task<IActionResult> CreateOrder(CreateOrderDto dto)
        {
            // ① 從 token 讀 UserId
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // ② 檢查有沒有商品
            if (dto.Items == null || dto.Items.Count == 0)
            {
                return BadRequest(new { message = "訂單沒有商品" });
            }

            // ③ 建立訂單主檔
            var order = new Order
            {
                UserId = userId,
                Status = "待付款",                    // 後端決定初始狀態
                OrderDate = DateTimeOffset.UtcNow,     // 現在時間
                ShipName = dto.ShipName,
                ShipAddress = dto.ShipAddress,
                ShipPhone = dto.ShipPhone,
                Freight = 0,                           // 運費，先簡化為 0（之後算）
                PaymentMethodId = 1,                   // 先寫死（貨到付款，之後處理）
            };

            // ④ 建立訂單明細（每個商品一筆，價格從資料庫查）
            foreach (var item in dto.Items)
            {
                // 從資料庫查這個規格的「真實單價」（不信任前端）
                var spec = await _context.ProductSpecification
                    .Include(s => s.Product)
                    .FirstOrDefaultAsync(s => s.ProductSpecificationId == item.ProductSpecificationId);

                if (spec == null)
                {
                    return BadRequest(new { message = $"找不到規格 {item.ProductSpecificationId}" });
                }

                var detail = new OrderDetail
                {
                    ProductSpecificationId = item.ProductSpecificationId,
                    Quantity = item.Quantity,
                    Price = spec.Product.Price,   // ★ 從資料庫查的真實價格
                };

                // 把明細掛到訂單底下（EF Core 會自動處理關聯）
                order.OrderDetail.Add(detail);
            }

            // ⑤ 一次存進資料庫（主檔 + 明細一起）
            _context.Order.Add(order);
            await _context.SaveChangesAsync();

            // ⑥ 清掉這次已下單的購物車項目
            // 拿出這次下單的所有規格 id
            var orderedSpecIds = dto.Items
                .Select(i => i.ProductSpecificationId)
                .ToList();

            // 找出這個使用者購物車裡、屬於這次下單的項目
            var cartItemsToRemove = await _context.Cart
                .Where(c => c.UserId == userId
                         && orderedSpecIds.Contains(c.ProductSpecificationId))
                .ToListAsync();

            // 刪除它們
            _context.Cart.RemoveRange(cartItemsToRemove);
            await _context.SaveChangesAsync();

            // 回傳新訂單的 id
            return Ok(new { message = "訂單建立成功", orderId = order.OrderId });
        }

        // GET api/order —— 拿「我的」訂單清單
        [HttpGet]
        public async Task<IActionResult> GetMyOrders()
        {
            // ① 從 token 讀 userId
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // ② 撈我的訂單，撈的時候要.Include 明細（才能算總金額）
            var orders = await _context.Order
              .Where(o => o.UserId == userId)
              .Include(o => o.OrderDetail)
              .Select(o => new OrderListDto
              {
                  OrderId = o.OrderId,
                  OrderDate = o.OrderDate,
                  Status = o.Status,
                  Total = o.OrderDetail.Sum(d => d.Price * d.Quantity)
              }).ToListAsync();

            return Ok(orders);
        }

        // GET api/order/id —— 拿訂單詳情
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrderdetail(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var orderdetail = await _context.Order
                .Where(o => o.OrderId == id && o.UserId == userId)
                .Include(o => o.OrderDetail)
                    .ThenInclude(d => d.ProductSpecification)
                    .ThenInclude(s => s.Product)
                .Select(o => new OrderDetailViewDto
                {
                    OrderId = o.OrderId,
                    OrderDate = o.OrderDate,
                    Status = o.Status,
                    ShipName = o.ShipName,
                    ShipAddress = o.ShipAddress,
                    ShipPhone = o.ShipPhone,
                    Total = o.OrderDetail.Sum(d => d.Price * d.Quantity),
                    Items = o.OrderDetail.Select(d => new OrderItemViewDto
                    {
                        OrderDetailId = d.OrderDetailId,
                        ProductName = d.ProductSpecification.Product.ProductName,
                        Color = d.ProductSpecification.Color,
                        Size = d.ProductSpecification.Size,
                        Quantity = d.Quantity,
                        Price = d.Price,
                    }).ToList(),
                }).FirstOrDefaultAsync();
            if (orderdetail == null) return NotFound();
            return Ok(orderdetail);
        }

        // PUT api/order/{id}/complete —— 確認收貨，訂單變已完成
        [HttpPut("{id}/complete")]
        public async Task<IActionResult> CompleteOrder(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var order = await _context.Order
                .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

            if (order == null) return NotFound(new { message = "找不到訂單" });

            // 只有「待出貨」能確認收貨
            if (order.Status != "待出貨")
            {
                return BadRequest(new { message = "此訂單目前狀態無法確認收貨" });
            }

            order.Status = "已完成";
            await _context.SaveChangesAsync();

            return Ok(new { message = "已確認收貨" });
        }
    }
}
