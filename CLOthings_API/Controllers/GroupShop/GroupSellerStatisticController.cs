using CLOthings_API.DTOs.GroupShop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("api/GroupSellerStatistic")]
[ApiController]
[Authorize(Roles = "Admin,SuperAdmin")] // 賣家端統計數據，僅限管理員查看
public class GroupSellerStatisticController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public GroupSellerStatisticController(CLOthingsContext context)
    {
        _context = context;
    }

    // GET: api/GroupSellerStatistic/product/5
    // 查某個商品逐日的統計明細（可以拿來畫折線圖）
    [HttpGet("product/{productId}")]
    public async Task<ActionResult<IEnumerable<GroupSellerStatisticDTO>>> GetByProduct(int productId)
    {
        var rows = await _context.GroupSellerStatistic
            .Include(s => s.GroupProduct)
            .Where(s => s.GroupProductId == productId)
            .OrderBy(s => s.StatisticDate)
            .ToListAsync();

        return Ok(rows.Select(ToDTO).ToList());
    }

    // GET: api/GroupSellerStatistic/summary
    // 每個商品的加總數字總覽（給表格用，一個商品一列）
    [HttpGet("summary")]
    public async Task<ActionResult<IEnumerable<GroupSellerStatisticSummaryDTO>>> GetSummary()
    {
        var rows = await _context.GroupSellerStatistic
            .Include(s => s.GroupProduct)
            .ToListAsync();

        var result = rows
            .GroupBy(s => new { s.GroupProductId, ProductName = s.GroupProduct.ProductName })
            .Select(g =>
            {
                var totalAddCart = g.Sum(x => x.AddCartCount ?? 0);
                var totalCheckout = g.Sum(x => x.CheckoutCount ?? 0);
                return new GroupSellerStatisticSummaryDTO
                {
                    GroupProductId = g.Key.GroupProductId,
                    ProductName = g.Key.ProductName,
                    TotalAddCartCount = totalAddCart,
                    TotalCheckoutCount = totalCheckout,
                    TotalViewCount = g.Sum(x => x.ViewCount ?? 0),
                    TotalFavorCount = g.Sum(x => x.FavorCount ?? 0),
                    ConversionRate = totalAddCart == 0 ? 0 : Math.Round((double)totalCheckout / totalAddCart, 4)
                };
            })
            .OrderByDescending(s => s.TotalCheckoutCount)
            .ToList();

        return Ok(result);
    }

    private static GroupSellerStatisticDTO ToDTO(GroupSellerStatistic s)
    {
        return new GroupSellerStatisticDTO
        {
            GroupProductId = s.GroupProductId,
            ProductName = s.GroupProduct?.ProductName,
            AddCartCount = s.AddCartCount ?? 0,
            CheckoutCount = s.CheckoutCount ?? 0,
            ViewCount = s.ViewCount ?? 0,
            FavorCount = s.FavorCount ?? 0,
            StatisticDate = s.StatisticDate.HasValue ? s.StatisticDate.Value.ToString("yyyy/MM/dd") : null
        };
    }
}
