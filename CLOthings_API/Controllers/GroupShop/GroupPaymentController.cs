using System.Security.Claims;
using System.Collections.Concurrent;
using CLOthings_API.DTOs.GroupShop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// 這支是「假的第三方金流商」：模擬使用者在銀行/金流頁面付款的過程。
// 待付款資料先放在記憶體裡（不用另外開資料表），後端重啟會清空，但這時候本來就還沒有真正的訂單，
// 頂多是使用者要重新走一次結帳，不會弄丟已經成立的訂單。
[Route("api/GroupPayment")]
[ApiController]
[Authorize(Roles = "User,SuperAdmin")] // 整支都是買家結帳流程，需要先登入
public class GroupPaymentController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public GroupPaymentController(CLOthingsContext context)
    {
        _context = context;
    }

    // 從 JWT 的 Claims 取得目前登入者的 UserId，不再讓前端（Vue）自己傳 UserId 過來
    // 這個方法只能在已經掛 [Authorize] 的 Controller/Action 裡呼叫，否則 Claims 裡不會有這筆資料
    private int GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : 0;
    }

    // 記憶體裡暫存「正在付款中」的資料：key 是 paymentId，value 是這筆付款要用的收件資訊
    // static + ConcurrentDictionary：讓同一個後端行程裡，不管哪個請求進來都能共用同一份資料，且執行緒安全
    // UserId 額外存起來（從 JWT 取得，不是前端傳的），確保之後查詢/確認付款時可以驗證身分
    private static readonly ConcurrentDictionary<string, PendingPaymentEntry> PendingPayments = new();

    private class PendingPaymentEntry
    {
        public int UserId { get; set; }
        public GroupCheckoutDTO Dto { get; set; }
    }

    // POST: api/GroupPayment/create
    // 結帳頁按下「前往付款」時呼叫這支：不會直接建立訂單，只是先把收件資訊記下來，換一個 paymentId
    [HttpPost("create")]
    public async Task<ActionResult<CreatePaymentResultDTO>> CreatePayment(GroupCheckoutDTO dto)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(dto.ShipName) || string.IsNullOrWhiteSpace(dto.ShipPhone) || string.IsNullOrWhiteSpace(dto.ShipAddress))
        {
            return BadRequest("請完整填寫收件人姓名、電話與地址");
        }

        var cartItems = await _context.GroupCart.Where(c => c.UserId == userId).ToListAsync();
        if (cartItems.Count == 0)
        {
            return BadRequest("購物車是空的，請先加入商品");
        }

        var amount = await CalculateAmountAsync(userId);

        var paymentId = Guid.NewGuid().ToString("N");
        PendingPayments[paymentId] = new PendingPaymentEntry { UserId = userId, Dto = dto };

        return Ok(new CreatePaymentResultDTO
        {
            PaymentId = paymentId,
            Amount = amount
        });
    }

    // GET: api/GroupPayment/5   (5 是 paymentId)
    // 「模擬付款頁」打開時呼叫這支，顯示金額、品項給使用者確認
    [HttpGet("{paymentId}")]
    public async Task<ActionResult<PendingPaymentDTO>> GetPending(string paymentId)
    {
        if (!PendingPayments.TryGetValue(paymentId, out var entry))
        {
            return NotFound("找不到這筆付款，可能已經處理過或已過期");
        }

        // 只能查自己建立的付款，SuperAdmin 不受限
        if (entry.UserId != GetUserId() && !User.IsInRole("SuperAdmin"))
        {
            return Forbid();
        }

        var dto = entry.Dto;

        var cartItems = await _context.GroupCart
            .Include(c => c.GroupProduct)
            .Where(c => c.UserId == entry.UserId)
            .ToListAsync();

        var orderedQtyMap = await GetOrderedQtyMapAsync();
        var tierMap = await _context.GroupDiscountStandard.ToListAsync();

        var items = cartItems.Select(c =>
        {
            var (unitPrice, _) = GroupCartController.ComputeUnitPrice(c.GroupProduct, tierMap, orderedQtyMap, c.GroupProductId, c.Quantity);
            return new GroupOrderItemDTO
            {
                GroupProductId = c.GroupProductId,
                ProductName = c.GroupProduct.ProductName,
                Quantity = c.Quantity,
                UnitPrice = unitPrice
            };
        }).ToList();

        return Ok(new PendingPaymentDTO
        {
            PaymentId = paymentId,
            Amount = await CalculateAmountAsync(entry.UserId),
            PaymentMethod = dto.PaymentMethod,
            Items = items
        });
    }

    // POST: api/GroupPayment/5/confirm   (5 是 paymentId)
    // 「模擬付款頁」按下「付款成功」或「付款失敗」後呼叫這支
    // 成功才會真的建立訂單、清空購物車；失敗的話什麼都不留，購物車保留讓使用者可以重新結帳
    [HttpPost("{paymentId}/confirm")]
    public async Task<ActionResult<GroupOrderDetailFullDTO>> ConfirmPayment(string paymentId, ConfirmPaymentDTO confirm)
    {
        if (!PendingPayments.TryGetValue(paymentId, out var entry))
        {
            return NotFound("找不到這筆付款，可能已經處理過或已過期");
        }

        // 只能確認自己建立的付款，SuperAdmin 不受限
        if (entry.UserId != GetUserId() && !User.IsInRole("SuperAdmin"))
        {
            return Forbid();
        }

        PendingPayments.TryRemove(paymentId, out _);

        if (!confirm.Success)
        {
            return Ok(new { paid = false, message = "付款失敗，訂單未成立，購物車商品仍保留" });
        }

        // 付款成功，這裡才是「真的」建立訂單的地方
        var order = await CreateOrderFromCartAsync(entry.UserId, entry.Dto);
        if (order == null)
        {
            return BadRequest("付款成功，但購物車已經是空的，可能是重複送出，請重新確認訂單");
        }

        return Ok(ToFullDTO(order));
    }

    // 付款成功後，把使用者購物車裡的內容真的轉成一筆訂單（邏輯跟原本 GroupOrderController.Checkout 相同）
    private async Task<GroupOrder> CreateOrderFromCartAsync(int userId, GroupCheckoutDTO dto)
    {
        var cartItems = await _context.GroupCart
            .Include(c => c.GroupProduct)
            .Where(c => c.UserId == userId)
            .ToListAsync();

        if (cartItems.Count == 0)
        {
            return null;
        }

        var orderedQtyMap = await GetOrderedQtyMapAsync();
        var tierMap = await _context.GroupDiscountStandard.ToListAsync();

        var priced = cartItems.Select(c =>
        {
            var (unitPrice, _) = GroupCartController.ComputeUnitPrice(c.GroupProduct, tierMap, orderedQtyMap, c.GroupProductId, c.Quantity);
            return new { Cart = c, UnitPrice = unitPrice };
        }).ToList();

        var subtotal = priced.Sum(p => p.UnitPrice * p.Cart.Quantity);
        var freight = subtotal >= 1000 ? 0 : 60;
        var grandTotal = subtotal + freight;

        var paymentMethod = await _context.GroupPaymentMethod.FirstOrDefaultAsync(p =>
            p.UserId == userId && p.Provider == dto.PaymentMethod);

        if (paymentMethod == null)
        {
            paymentMethod = new GroupPaymentMethod
            {
                UserId = userId,
                Provider = dto.PaymentMethod,
                Token = "N/A",
                CardBrand = dto.PaymentMethod,
                ExpireAt = "9999/12",
                IsDefault = false
            };
            _context.GroupPaymentMethod.Add(paymentMethod);
            await _context.SaveChangesAsync();
        }

        var order = new GroupOrder
        {
            UserId = userId,
            Status = "進行中 (組團中)",
            TotalPrice = grandTotal,
            OrderDate = DateTimeOffset.Now,
            PickupMethod = dto.PickupMethod,
            ShipName = dto.ShipName,
            ShipAddress = dto.ShipAddress,
            ShipPhone = dto.ShipPhone,
            Freight = freight,
            PaymentMethodId = paymentMethod.GroupPaymentMethodId
        };

        foreach (var p in priced)
        {
            order.GroupOrderDetail.Add(new GroupOrderDetail
            {
                GroupProductId = p.Cart.GroupProductId,
                GroupProductSpecificationId = p.Cart.GroupProductSpecificationId,
                Quantity = p.Cart.Quantity,
                Price = p.UnitPrice,
                Discount = p.UnitPrice < p.Cart.GroupProduct.Price
                    ? Math.Round(p.UnitPrice / p.Cart.GroupProduct.Price, 4)
                    : (decimal?)null
            });
        }

        _context.GroupOrder.Add(order);
        _context.GroupCart.RemoveRange(cartItems);
        await _context.SaveChangesAsync();

        // 埋點：結帳成功後，這筆訂單裡買到的每個商品，今天的結帳次數各 +1（跟原本 Checkout 邏輯一致）
        foreach (var p in priced)
        {
            await IncrementStatAsync(p.Cart.GroupProductId, s => s.CheckoutCount = (s.CheckoutCount ?? 0) + 1);
        }

        var saved = await _context.GroupOrder
            .Include(o => o.GroupOrderDetail)
                .ThenInclude(d => d.GroupProduct)
            .FirstAsync(o => o.GroupOrderId == order.GroupOrderId);

        return saved;
    }

    // 算出使用者目前購物車的應付總額（金額顯示在「模擬付款頁」用）
    private async Task<int> CalculateAmountAsync(int userId)
    {
        var cartItems = await _context.GroupCart
            .Include(c => c.GroupProduct)
            .Where(c => c.UserId == userId)
            .ToListAsync();

        var orderedQtyMap = await GetOrderedQtyMapAsync();
        var tierMap = await _context.GroupDiscountStandard.ToListAsync();

        var subtotal = cartItems.Sum(c =>
        {
            var (unitPrice, _) = GroupCartController.ComputeUnitPrice(c.GroupProduct, tierMap, orderedQtyMap, c.GroupProductId, c.Quantity);
            return unitPrice * c.Quantity;
        });

        var freight = subtotal >= 1000 ? 0 : 60;
        return subtotal + freight;
    }

    private async Task<Dictionary<int, int>> GetOrderedQtyMapAsync()
    {
        return await _context.GroupOrderDetail
            .Where(d => !d.GroupOrder.Status.Contains("取消"))
            .GroupBy(d => d.GroupProductId)
            .Select(g => new { GroupProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.GroupProductId, x => x.Qty);
    }

    private async Task IncrementStatAsync(int productId, Action<GroupSellerStatistic> increment)
    {
        var today = DateTimeOffset.Now.Date;
        var stat = await _context.GroupSellerStatistic.FirstOrDefaultAsync(s =>
            s.GroupProductId == productId &&
            s.StatisticDate.HasValue &&
            s.StatisticDate.Value.Date == today);

        if (stat == null)
        {
            var product = await _context.GroupProduct.FindAsync(productId);
            if (product == null) return;

            stat = new GroupSellerStatistic
            {
                GroupProductId = productId,
                GroupSupplierId = product.GroupSupplierId,
                AddCartCount = 0,
                CheckoutCount = 0,
                ViewCount = 0,
                FavorCount = 0,
                StatisticDate = DateTimeOffset.Now
            };
            _context.GroupSellerStatistic.Add(stat);
        }

        increment(stat);
        await _context.SaveChangesAsync();
    }

    private static GroupOrderDetailFullDTO ToFullDTO(GroupOrder o)
    {
        return new GroupOrderDetailFullDTO
        {
            GroupOrderId = o.GroupOrderId,
            ProductName = string.Join("、", o.GroupOrderDetail.Select(d => $"{d.GroupProduct.ProductName} x{d.Quantity}")),
            Status = o.Status,
            TotalPrice = (int)o.TotalPrice,
            OrderDate = o.OrderDate.ToString("yyyy/MM/dd"),
            ShipName = o.ShipName,
            ShipPhone = o.ShipPhone,
            ShipAddress = o.ShipAddress,
            PickupMethod = o.PickupMethod,
            Freight = (int)o.Freight,
            Items = o.GroupOrderDetail.Select(d => new GroupOrderItemDTO
            {
                GroupProductId = d.GroupProductId,
                ProductName = d.GroupProduct.ProductName,
                Quantity = d.Quantity,
                UnitPrice = (int)d.Price
            }).ToList()
        };
    }
}
