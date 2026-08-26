using System.Security.Claims;
using CLOthings_API.DTOs.GroupShop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

[Route("api/GroupOrder")]
[ApiController]
public class GroupOrderController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public GroupOrderController(CLOthingsContext context)
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

    // GET: api/GroupOrder/mine，對應「我的團購訂單」列表頁
    // UserId 一律從 JWT 取得，不再由前端指定
    [HttpGet("mine")]
    [Authorize(Roles = "User,SuperAdmin")]
    public async Task<ActionResult<IEnumerable<GroupOrderListDTO>>> GetOrders()
    {
        var userId = GetUserId();

        var orders = await _context.GroupOrder
            .Include(o => o.GroupOrderDetail)
                .ThenInclude(d => d.GroupProduct)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        var result = orders.Select(ToListDTO).ToList();
        return Ok(result);
    }

    // GET: api/GroupOrder/detail/5   (5 是 GroupOrderId)，給「編輯訂單」Modal 用
    [HttpGet("detail/{orderId}")]
    [Authorize(Roles = "User,SuperAdmin")]
    public async Task<ActionResult<GroupOrderDetailFullDTO>> GetOrderDetail(int orderId)
    {
        var order = await _context.GroupOrder
            .Include(o => o.GroupOrderDetail)
                .ThenInclude(d => d.GroupProduct)
            .FirstOrDefaultAsync(o => o.GroupOrderId == orderId);

        if (order == null)
        {
            return NotFound();
        }

        // 只能看自己的訂單，SuperAdmin 不受限
        if (order.UserId != GetUserId() && !User.IsInRole("SuperAdmin"))
        {
            return Forbid();
        }

        return Ok(ToFullDTO(order));
    }

    // POST: api/GroupOrder/checkout
    // 把使用者購物車（GroupCart）裡的商品結成一筆訂單，成功後會清空購物車
    [HttpPost("checkout")]
    [Authorize(Roles = "User,SuperAdmin")]
    [EnableRateLimiting("group")] // 每個來源 10 秒內最多 10 次結帳請求，防止被程式狂刷
    public async Task<ActionResult<GroupOrderDetailFullDTO>> Checkout(GroupCheckoutDTO dto)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(dto.ShipName) || string.IsNullOrWhiteSpace(dto.ShipPhone) || string.IsNullOrWhiteSpace(dto.ShipAddress))
        {
            return BadRequest("請完整填寫收件人姓名、電話與地址");
        }

        var cartItems = await _context.GroupCart
            .Include(c => c.GroupProduct)
            .Where(c => c.UserId == userId)
            .ToListAsync();

        if (cartItems.Count == 0)
        {
            return BadRequest("購物車是空的，請先加入商品");
        }

        var orderedQtyMap = await _context.GroupOrderDetail
            .Where(d => !d.GroupOrder.Status.Contains("取消"))
            .GroupBy(d => d.GroupProductId)
            .Select(g => new { GroupProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.GroupProductId, x => x.Qty);

        var tierMap = await _context.GroupDiscountStandard.ToListAsync();

        // 逐項算出目前應該用的單價（已成立件數 + 這筆購物車件數）
        var priced = cartItems.Select(c =>
        {
            var (unitPrice, _) = GroupCartController.ComputeUnitPrice(c.GroupProduct, tierMap, orderedQtyMap, c.GroupProductId, c.Quantity);
            return new { Cart = c, UnitPrice = unitPrice };
        }).ToList();

        var subtotal = priced.Sum(p => p.UnitPrice * p.Cart.Quantity);
        var freight = subtotal >= 1000 ? 0 : 60;
        var grandTotal = subtotal + freight;

        // 目前沒有真正的金流串接，這裡把使用者選的付款方式當作一筆 GroupPaymentMethod 記錄（找不到就新增一筆）
        var paymentMethod = await _context.GroupPaymentMethod.FirstOrDefaultAsync(p =>
            p.UserId == userId && p.Provider == dto.PaymentMethod);

        if (paymentMethod == null)
        {
            paymentMethod = new GroupPaymentMethod
            {
                UserId = userId,
                Provider = dto.PaymentMethod,
                Token = "N/A", // 資料庫這個欄位是 NOT NULL，目前沒有真的串金流所以沒有真正的 token，先用佔位值頂著
                CardBrand = dto.PaymentMethod, // 同上，資料庫這個欄位是 NOT NULL
                ExpireAt = "9999/12", // 同上，資料庫這個欄位是 NOT NULL 且是字串型別
                IsDefault = false
            };
            _context.GroupPaymentMethod.Add(paymentMethod);
            await _context.SaveChangesAsync(); // 先存檔才能拿到 PaymentMethodId
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

        // 訂單建立成功後清空這位使用者的購物車
        _context.GroupCart.RemoveRange(cartItems);

        await _context.SaveChangesAsync();

        // 埋點：結帳成功後，這筆訂單裡買到的每個商品，今天的結帳次數各 +1
        foreach (var p in priced)
        {
            await IncrementStatAsync(p.Cart.GroupProductId, s => s.CheckoutCount = (s.CheckoutCount ?? 0) + 1);
        }

        // 重新查一次，把 GroupProduct 名稱帶進來組成回傳的 DTO
        var saved = await _context.GroupOrder
            .Include(o => o.GroupOrderDetail)
                .ThenInclude(d => d.GroupProduct)
            .FirstAsync(o => o.GroupOrderId == order.GroupOrderId);

        return Ok(ToFullDTO(saved));
    }

    // PUT: api/GroupOrder/5/cancel   (5 是 GroupOrderId)
    [HttpPut("{orderId}/cancel")]
    [Authorize(Roles = "User,SuperAdmin")]
    public async Task<IActionResult> CancelOrder(int orderId)
    {
        var order = await _context.GroupOrder.FindAsync(orderId);
        if (order == null)
        {
            return NotFound();
        }

        // 只能取消自己的訂單，SuperAdmin 不受限
        if (order.UserId != GetUserId() && !User.IsInRole("SuperAdmin"))
        {
            return Forbid();
        }

        if (order.Status == "已取消")
        {
            return NoContent(); // 已經取消過了，避免重複扣件數，直接回傳成功
        }

        // 這裡不用像前端那樣另外扣「已累計件數」，因為 GroupProductController 查詢時
        // 本來就只加總「非已取消」訂單的件數，狀態一改，商品頁的已訂購件數會自動同步復原
        order.Status = "已取消";
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // PUT: api/GroupOrder/5   (5 是 GroupOrderId)，對應「編輯訂單」Modal 的儲存
    [HttpPut("{orderId}")]
    [Authorize(Roles = "User,SuperAdmin")]
    public async Task<ActionResult<GroupOrderDetailFullDTO>> EditOrder(int orderId, EditGroupOrderDTO dto)
    {
        var order = await _context.GroupOrder
            .Include(o => o.GroupOrderDetail)
                .ThenInclude(d => d.GroupProduct)
            .FirstOrDefaultAsync(o => o.GroupOrderId == orderId);

        if (order == null)
        {
            return NotFound();
        }

        // 只能編輯自己的訂單，SuperAdmin 不受限
        if (order.UserId != GetUserId() && !User.IsInRole("SuperAdmin"))
        {
            return Forbid();
        }

        if (order.Status == "已取消")
        {
            return BadRequest("已取消的訂單不能編輯");
        }

        order.ShipName = dto.ShipName;

        var orderedQtyMap = await _context.GroupOrderDetail
            .Where(d => !d.GroupOrder.Status.Contains("取消") && d.GroupOrderId != orderId)
            .GroupBy(d => d.GroupProductId)
            .Select(g => new { GroupProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.GroupProductId, x => x.Qty);

        var tierMap = await _context.GroupDiscountStandard.ToListAsync();

        foreach (var editItem in dto.Items)
        {
            var detail = order.GroupOrderDetail.FirstOrDefault(d => d.GroupProductId == editItem.GroupProductId);
            if (detail == null) continue;

            var newQty = Math.Max(1, editItem.Quantity);
            var (unitPrice, _) = GroupCartController.ComputeUnitPrice(detail.GroupProduct, tierMap, orderedQtyMap, detail.GroupProductId, newQty);

            detail.Quantity = newQty;
            detail.Price = unitPrice;

            // 這個商品在「其他訂單」的累計件數也要一併加回去，才不會讓下一個品項算錯
            orderedQtyMap[detail.GroupProductId] = orderedQtyMap.GetValueOrDefault(detail.GroupProductId) + newQty;
        }

        var subtotal = order.GroupOrderDetail.Sum(d => d.Price * d.Quantity);
        order.Freight = subtotal >= 1000 ? 0 : 60;
        order.TotalPrice = subtotal + order.Freight;

        await _context.SaveChangesAsync();

        return Ok(ToFullDTO(order));
    }

    // ================= 以下是管理端（訂單列表 + 更新狀態 + 指派物流）的 API =================

    // GET: api/GroupOrder/admin/all
    // status 選填：帶了就只回傳該狀態的訂單，例如 ?status=進行中 (組團中)
    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<IEnumerable<GroupOrderAdminListDTO>>> GetAllOrders([FromQuery] string status = null)
    {
        var query = _context.GroupOrder
            .Include(o => o.GroupOrderDetail)
                .ThenInclude(d => d.GroupProduct)
            .Include(o => o.GroupShipper)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            // 改用 Contains 而不是完全比對：結算作業流團自動取消訂單時，存的狀態字串是
            // 「已取消（團購未成立，已退款）」，跟篩選選單裡單純的「已取消」不會完全比對成功，
            // 篩「已取消」時這些訂單會被漏掉。改成 Contains 才能把這種帶原因的取消狀態也篩出來，
            // 跟畫面上判斷徽章顏色（getBadgeClass）用的邏輯保持一致。
            query = query.Where(o => o.Status.Contains(status));
        }

        var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();
        return Ok(orders.Select(ToAdminListDTO).ToList());
    }

    // PUT: api/GroupOrder/5/status   (5 是 GroupOrderId)
    // 後台可以手動切換的訂單狀態，要跟前端 GroupOrderAdminView 的下拉選單保持一致。
    // 「已取消（團購未成立，已退款）」這種帶原因的取消狀態是結算作業自動產生的，不開放後台手動選
    private static readonly HashSet<string> AllowedStatuses = new()
    {
        "進行中 (組團中)",
        "已成團 (備貨中)",
        "已完成",
        "已取消"
    };

    [HttpPut("{orderId}/status")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> UpdateStatus(int orderId, UpdateOrderStatusDTO dto)
    {
        var order = await _context.GroupOrder.FindAsync(orderId);
        if (order == null)
        {
            return NotFound();
        }

        // 只能改成前端下拉選單有的那幾種狀態，避免打錯字或帶入奇怪字串，讓「已取消」相關的字串比對邏輯失效
        if (string.IsNullOrWhiteSpace(dto.Status) || !AllowedStatuses.Contains(dto.Status))
        {
            return BadRequest("不合法的訂單狀態");
        }

        order.Status = dto.Status;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // PUT: api/GroupOrder/5/shipper   (5 是 GroupOrderId)
    [HttpPut("{orderId}/shipper")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> AssignShipper(int orderId, AssignShipperDTO dto)
    {
        var order = await _context.GroupOrder.FindAsync(orderId);
        if (order == null)
        {
            return NotFound();
        }

        // 已取消的訂單不需要（也不該）再指派物流商
        if (order.Status.Contains("取消"))
        {
            return BadRequest("已取消的訂單不能指派物流商");
        }

        var shipperExists = await _context.GroupShipper.AnyAsync(s => s.GroupShipperId == dto.GroupShipperId);
        if (!shipperExists)
        {
            return BadRequest("找不到這個物流商");
        }

        order.GroupShipperId = dto.GroupShipperId;
        order.ShipperDate = dto.ShipperDate ?? DateTimeOffset.Now;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static GroupOrderAdminListDTO ToAdminListDTO(GroupOrder o)
    {
        return new GroupOrderAdminListDTO
        {
            GroupOrderId = o.GroupOrderId,
            UserId = o.UserId,
            ProductName = string.Join("、", o.GroupOrderDetail.Select(d => $"{d.GroupProduct.ProductName} x{d.Quantity}")),
            Status = o.Status,
            TotalPrice = (int)o.TotalPrice,
            OrderDate = o.OrderDate.ToString("yyyy/MM/dd HH:mm"),
            PickupMethod = o.PickupMethod,
            ShipName = o.ShipName,
            ShipPhone = o.ShipPhone,
            ShipAddress = o.ShipAddress,
            GroupShipperId = o.GroupShipperId,
            ShipperName = o.GroupShipper != null ? o.GroupShipper.ShipperName : null,
            ShipperDate = o.ShipperDate.HasValue ? o.ShipperDate.Value.ToString("yyyy/MM/dd") : null
        };
    }

    // 埋點小工具：找出「今天」這個商品的統計列，沒有就新增一筆，再依傳進來的方式累加對應欄位
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

    // ---- 以下是把 GroupOrder 轉成前端要的 DTO 格式 ----

    private static GroupOrderListDTO ToListDTO(GroupOrder o)
    {
        return new GroupOrderListDTO
        {
            GroupOrderId = o.GroupOrderId,
            ProductName = string.Join("、", o.GroupOrderDetail.Select(d => $"{d.GroupProduct.ProductName} x{d.Quantity}")),
            Status = o.Status,
            TotalPrice = (int)o.TotalPrice,
            OrderDate = o.OrderDate.ToString("yyyy/MM/dd"),
            ShipName = o.ShipName
        };
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
