using CLOthings_API.DTOs.GroupShop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// 管理端「結算」功能：檢查所有已經過了 SalesEnd 截止日、但還沒結算過的商品，
// 達到第一階層件數 → 商品成團；沒達到 → 商品流團，相關訂單自動取消並記錄一封（假的）退款通知信
[Route("api/GroupSettlement")]
[ApiController]
public class GroupSettlementController : ControllerBase
{
    private readonly CLOthingsContext _context;
    private readonly ILogger<GroupSettlementController> _logger;

    public GroupSettlementController(CLOthingsContext context, ILogger<GroupSettlementController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // POST: api/GroupSettlement/run
    [HttpPost("run")]
    public async Task<ActionResult<GroupSettlementResultDTO>> Run()
    {
        var result = new GroupSettlementResultDTO();
        var now = DateTimeOffset.Now;

        // 找出「已經過截止日、但還沒結算過」的商品：Status 還不是「已成團」也不是「已流團」
        var duedProducts = await _context.GroupProduct
            .Where(p => p.SalesEnd != null && p.SalesEnd < now && p.Status != "已成團" && p.Status != "已流團")
            .ToListAsync();

        if (duedProducts.Count == 0)
        {
            return Ok(result); // 沒有任何商品到期需要結算，回傳空結果
        }

        var orderedQtyMap = await _context.GroupOrderDetail
            .Where(d => !d.GroupOrder.Status.Contains("取消"))
            .GroupBy(d => d.GroupProductId)
            .Select(g => new { GroupProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.GroupProductId, x => x.Qty);

        var allTiers = await _context.GroupDiscountStandard.ToListAsync();

        foreach (var product in duedProducts)
        {
            var orderedQty = orderedQtyMap.TryGetValue(product.GroupProductId, out var qty) ? qty : 0;

            // 第一階層（門檻最低的那一階）決定「成團」的最低件數
            var firstTier = allTiers
                .Where(t => t.GroupProductId == product.GroupProductId)
                .Select(t => int.TryParse(t.ThresholdCount, out var n) ? n : int.MaxValue)
                .DefaultIfEmpty(int.MaxValue)
                .Min();

            if (firstTier == int.MaxValue)
            {
                // 這個商品完全沒有設定階層，沒辦法判斷有沒有成團，先跳過，不列入這次結算結果
                continue;
            }

            if (orderedQty >= firstTier)
            {
                product.Status = "已成團";
                result.SucceededProducts.Add(new SettledProductDTO
                {
                    GroupProductId = product.GroupProductId,
                    ProductName = product.ProductName,
                    OrderedQty = orderedQty,
                    RequiredQty = firstTier
                });
            }
            else
            {
                product.Status = "已流團";
                result.FailedProducts.Add(new SettledProductDTO
                {
                    GroupProductId = product.GroupProductId,
                    ProductName = product.ProductName,
                    OrderedQty = orderedQty,
                    RequiredQty = firstTier
                });
            }
        }

        await _context.SaveChangesAsync(); // 先把商品的成團/流團狀態存檔

        // 找出所有「包含這次結算商品」、且還沒被取消的訂單，逐筆檢查怎麼處理
        var duedProductIds = duedProducts.Select(p => p.GroupProductId).ToHashSet();

        var affectedOrders = await _context.GroupOrder
            .Include(o => o.GroupOrderDetail)
                .ThenInclude(d => d.GroupProduct)
            .Include(o => o.User)
            .Where(o => !o.Status.Contains("取消") && o.GroupOrderDetail.Any(d => duedProductIds.Contains(d.GroupProductId)))
            .ToListAsync();

        foreach (var order in affectedOrders)
        {
            var hasFailedItem = order.GroupOrderDetail.Any(d => d.GroupProduct.Status == "已流團");
            var allSucceeded = order.GroupOrderDetail.All(d => d.GroupProduct.Status == "已成團");

            if (hasFailedItem)
            {
                // 只要這筆訂單裡有任何一項商品流團，整筆訂單就取消退款（簡化規則：不做部分退款）
                order.Status = "已取消（團購未成立，已退款）";

                var notice = new RefundNoticeDTO
                {
                    GroupOrderId = order.GroupOrderId,
                    ToEmail = order.User?.Email,
                    Subject = $"【CLOthings 團購】訂單 #{order.GroupOrderId} 未成團通知與退款",
                    Body = $"親愛的 {order.ShipName} 您好，您訂購的商品因未達成團門檻，訂單 #{order.GroupOrderId}（金額 NT$ {order.TotalPrice}）已為您取消，退款將於 3~5 個工作天內原路退回。"
                };
                result.RefundNotices.Add(notice);

                // 假寄信：先寫進後端 log，之後要串真的 SMTP 只要把這行換成真正寄信的程式碼即可
                _logger.LogInformation("[假信件] 收件人：{Email}，主旨：{Subject}，內容：{Body}", notice.ToEmail, notice.Subject, notice.Body);
            }
            else if (allSucceeded)
            {
                // 這筆訂單裡的商品全部都成團了，訂單狀態轉為備貨中
                order.Status = "已成團 (備貨中)";
                result.ConfirmedOrderIds.Add(order.GroupOrderId);
            }
            // 其餘情況（訂單裡還有商品尚未到期、還不能判定）先維持原狀，等下次結算再檢查
        }

        await _context.SaveChangesAsync();

        return Ok(result);
    }
}
