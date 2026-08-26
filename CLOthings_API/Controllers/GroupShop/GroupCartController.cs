using System.Security.Claims;
using CLOthings_API.DTOs.GroupShop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

[Route("api/GroupCart")]
[ApiController]
[Authorize(Roles = "User,SuperAdmin")] // 購物車整支都是買家個人資料，需要先登入
public class GroupCartController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public GroupCartController(CLOthingsContext context)
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

    // GET: api/GroupCart
    // UserId 一律從 JWT 取得，不再由前端指定，避免有人改 userId 就能看到別人的購物車
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GroupCartItemDTO>>> GetCart()
    {
        var userId = GetUserId();

        var cartRows = await _context.GroupCart
            .Include(c => c.GroupProduct)
            .Where(c => c.UserId == userId)
            .ToListAsync();

        if (cartRows.Count == 0)
        {
            return Ok(new List<GroupCartItemDTO>());
        }

        var orderedQtyMap = await GetOrderedQtyMapAsync();
        var tierMap = await _context.GroupDiscountStandard.ToListAsync();

        var result = cartRows.Select(c =>
        {
            var (unitPrice, unlocked) = ComputeUnitPrice(c.GroupProduct, tierMap, orderedQtyMap, c.GroupProductId, c.Quantity);
            return new GroupCartItemDTO
            {
                GroupCartId = c.GroupCartId,
                GroupProductId = c.GroupProductId,
                GroupProductSpecificationId = c.GroupProductSpecificationId,
                Name = c.GroupProduct.ProductName,
                ImageUrl = c.GroupProduct.ProductImg,
                ListPrice = (int)c.GroupProduct.Price,
                UnitPrice = unitPrice,
                Unlocked = unlocked,
                Quantity = c.Quantity
            };
        }).ToList();

        return Ok(result);
    }

    // POST: api/GroupCart
    // 加入購物車：同商品同規格已經在購物車裡的話，數量累加；沒有的話新增一筆
    [HttpPost]
    [EnableRateLimiting("group")] // 防止有人寫程式狂刷加入購物車
    public async Task<ActionResult<GroupCartItemDTO>> AddToCart(AddGroupCartDTO dto)
    {
        var userId = GetUserId();

        // 數量至少為 1，避免直接呼叫 API 帶 0 或負數進來，弄亂團購已訂購件數的計算
        dto.Quantity = Math.Max(1, dto.Quantity);

        var product = await _context.GroupProduct.FindAsync(dto.GroupProductId);
        if (product == null)
        {
            return NotFound("找不到這個團購商品");
        }
        // 已下架商品不能再被加入購物車，避免有人拿舊網址/舊分享連結、或商品下架前就開著的分頁，
        // 繞過商品列表/詳情頁的過濾直接把已下架商品加進購物車
        if (product.Status != "上架中")
        {
            return BadRequest("這個商品已經下架，無法加入購物車");
        }

        // 已下架/已成團/已流團的商品不能再加入購物車
        if (product.Status != "上架中")
        {
            return BadRequest("這個商品目前無法加入購物車（已下架或團購已結束）");
        }

        // 前端目前還沒有尺寸/顏色選擇 UI，沒有指定規格的話就用該商品的第一個規格
        var specificationId = dto.GroupProductSpecificationId;
        if (specificationId == null)
        {
            var defaultSpec = await _context.GroupProductSpecification
                .Where(s => s.GroupProductId == dto.GroupProductId)
                .Select(s => (int?)s.GroupProductSpecificationId)
                .FirstOrDefaultAsync();

            if (defaultSpec == null)
            {
                return BadRequest("這個商品還沒有建立任何規格（GroupProductSpecification），請先建立至少一筆規格資料");
            }
            specificationId = defaultSpec;
        }
        else
        {
            // 前端如果有指定規格，確認這個規格真的屬於這個商品，避免把 A 商品的規格掛到 B 商品的購物車項目上
            var specBelongsToProduct = await _context.GroupProductSpecification
                .AnyAsync(s => s.GroupProductSpecificationId == specificationId && s.GroupProductId == dto.GroupProductId);

            if (!specBelongsToProduct)
            {
                return BadRequest("指定的規格不屬於這個商品");
            }
        }

        var existing = await _context.GroupCart.FirstOrDefaultAsync(c =>
            c.UserId == userId &&
            c.GroupProductId == dto.GroupProductId &&
            c.GroupProductSpecificationId == specificationId);

        if (existing != null)
        {
            existing.Quantity += dto.Quantity;
        }
        else
        {
            existing = new GroupCart
            {
                UserId = userId,
                GroupProductId = dto.GroupProductId,
                GroupProductSpecificationId = specificationId.Value,
                Quantity = dto.Quantity,
                AddDate = DateTimeOffset.Now
            };
            _context.GroupCart.Add(existing);
        }

        await _context.SaveChangesAsync();

        // 埋點：每次成功加入購物車，就把今天這個商品的加入購物車次數 +1
        await IncrementStatAsync(dto.GroupProductId, s => s.AddCartCount = (s.AddCartCount ?? 0) + 1);

        var orderedQtyMap = await GetOrderedQtyMapAsync();
        var tierMap = await _context.GroupDiscountStandard.Where(t => t.GroupProductId == dto.GroupProductId).ToListAsync();
        var (unitPrice, unlocked) = ComputeUnitPrice(product, tierMap, orderedQtyMap, dto.GroupProductId, existing.Quantity);

        return Ok(new GroupCartItemDTO
        {
            GroupCartId = existing.GroupCartId,
            GroupProductId = existing.GroupProductId,
            GroupProductSpecificationId = existing.GroupProductSpecificationId,
            Name = product.ProductName,
            ImageUrl = product.ProductImg,
            ListPrice = (int)product.Price,
            UnitPrice = unitPrice,
            Unlocked = unlocked,
            Quantity = existing.Quantity
        });
    }

    // PUT: api/GroupCart/5   (5 是 GroupCartId)，對應購物車頁調整數量的輸入框
    [HttpPut("{groupCartId}")]
    public async Task<IActionResult> UpdateQuantity(int groupCartId, UpdateGroupCartDTO dto)
    {
        var cart = await _context.GroupCart.FindAsync(groupCartId);
        if (cart == null)
        {
            return NotFound();
        }

        // 只能改自己的購物車項目，避免有人拿別人的 groupCartId 亂改數量
        if (cart.UserId != GetUserId())
        {
            return Forbid();
        }

        // 數量至少為 1，避免傳 0 或負數進來
        cart.Quantity = Math.Max(1, dto.Quantity);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/GroupCart/5   (5 是 GroupCartId)
    [HttpDelete("{groupCartId}")]
    public async Task<IActionResult> RemoveItem(int groupCartId)
    {
        var cart = await _context.GroupCart.FindAsync(groupCartId);
        if (cart == null)
        {
            return NotFound();
        }

        // 只能刪自己的購物車項目
        if (cart.UserId != GetUserId())
        {
            return Forbid();
        }

        _context.GroupCart.Remove(cart);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/GroupCart/me，結帳成功後清空「自己」購物車用，UserId 一樣從 JWT 取得
    [HttpDelete("me")]
    public async Task<IActionResult> ClearCart()
    {
        var userId = GetUserId();
        var items = await _context.GroupCart.Where(c => c.UserId == userId).ToListAsync();
        _context.GroupCart.RemoveRange(items);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // 查出每個商品目前「非已取消」訂單累計的件數
    private async Task<Dictionary<int, int>> GetOrderedQtyMapAsync()
    {
        return await _context.GroupOrderDetail
            .Where(d => !d.GroupOrder.Status.Contains("取消"))
            .GroupBy(d => d.GroupProductId)
            .Select(g => new { GroupProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.GroupProductId, x => x.Qty);
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

    // 依「已成立訂單件數 + 這筆購物車件數」算出目前應該用的團購單價
    // 邏輯跟前端 groupCart.js 的 unitPriceOf 一致：有解鎖階層就用團購價，沒有就用原價
    public static (int unitPrice, bool unlocked) ComputeUnitPrice(
        GroupProduct product,
        List<GroupDiscountStandard> tiers,
        Dictionary<int, int> orderedQtyMap,
        int groupProductId,
        int cartQty)
    {
        var committedQty = orderedQtyMap.TryGetValue(groupProductId, out var q) ? q : 0;
        var totalQty = committedQty + cartQty;

        var applicableTiers = tiers
            .Where(t => t.GroupProductId == groupProductId)
            .Select(t => new { Qty = int.TryParse(t.ThresholdCount, out var n) ? n : 0, Rate = t.DiscountRate ?? 1m })
            .OrderBy(t => t.Qty)
            .ToList();

        decimal? unlockedRate = null;
        foreach (var t in applicableTiers)
        {
            if (totalQty >= t.Qty) unlockedRate = t.Rate;
        }

        var unitPrice = unlockedRate.HasValue
            ? (int)Math.Round(product.Price * unlockedRate.Value)
            : (int)product.Price;

        return (unitPrice, unlockedRate.HasValue);
    }
}
