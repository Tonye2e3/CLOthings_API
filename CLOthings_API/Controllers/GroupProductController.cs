using CLOthings_API.DTOs.GroupShop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("api/GroupProduct")]
[ApiController]
public class GroupProductController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public GroupProductController(CLOthingsContext context)
    {
        _context = context;
    }

    // GET: api/GroupProduct
    // keyword 選填，對應前端商品列表頁的搜尋框（商品名稱模糊搜尋）
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GroupProductDTO>>> GetGroupProducts([FromQuery] string keyword = null)
    {
        var query = _context.GroupProduct.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(p => p.ProductName.Contains(keyword));
        }

        var products = await query.ToListAsync();

        // 每個商品「已成立訂單」累計件數：非已取消的訂單裡，該商品的 Quantity 加總
        var orderedQtyMap = await GetOrderedQtyMapAsync();

        // 所有商品的團購階層，一次查出來後在記憶體裡分組，避免每個商品都查一次資料庫
        var tierMap = await _context.GroupDiscountStandard
            .OrderBy(t => t.GroupProductId)
            .ToListAsync();

        var result = products
            .Select(p => BuildProductDTO(p, orderedQtyMap, tierMap))
            .ToList();

        return Ok(result);
    }

    // GET: api/GroupProduct/5
    [HttpGet("{id}")]
    public async Task<ActionResult<GroupProductDTO>> GetGroupProduct(int id)
    {
        var product = await _context.GroupProduct.FindAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        var orderedQtyMap = await GetOrderedQtyMapAsync();
        var tiers = await _context.GroupDiscountStandard
            .Where(t => t.GroupProductId == id)
            .ToListAsync();

        return Ok(BuildProductDTO(product, orderedQtyMap, tiers));
    }

    // 組出前端要用的 GroupProductDTO：把原始的 GroupProduct + GroupDiscountStandard 資料，
    // 換算成「已訂購件數」跟「每個階層的實際單價」
    private GroupProductDTO BuildProductDTO(GroupProduct p, Dictionary<int, int> orderedQtyMap, List<GroupDiscountStandard> allTiers)
    {
        var orderedQty = orderedQtyMap.TryGetValue(p.GroupProductId, out var qty) ? qty : 0;

        var tiers = allTiers
            .Where(t => t.GroupProductId == p.GroupProductId)
            .Select(t => new GroupProductTierDTO
            {
                Qty = ParseThresholdCount(t.ThresholdCount),
                Discount = t.DiscountRate ?? 1m,
                UnitPrice = (int)Math.Round(p.Price * (t.DiscountRate ?? 1m))
            })
            .OrderBy(t => t.Qty)
            .ToList();

        return new GroupProductDTO
        {
            Id = p.GroupProductId,
            Name = p.ProductName,
            ImageUrl = p.ProductImg,
            ListPrice = (int)p.Price,
            Intro = p.Description,
            Status = p.Status,
            OrderedQty = orderedQty,
            Tiers = tiers
        };
    }

    // ThresholdCount 在資料庫裡存的是字串，這裡安全轉成數字，轉不了就當 0
    private static int ParseThresholdCount(string thresholdCount)
    {
        return int.TryParse(thresholdCount, out var n) ? n : 0;
    }

    // 查出每個商品目前「非已取消」訂單累計的件數
    private async Task<Dictionary<int, int>> GetOrderedQtyMapAsync()
    {
        return await _context.GroupOrderDetail
            .Where(d => d.GroupOrder.Status != "已取消")
            .GroupBy(d => d.GroupProductId)
            .Select(g => new { GroupProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.GroupProductId, x => x.Qty);
    }
}
