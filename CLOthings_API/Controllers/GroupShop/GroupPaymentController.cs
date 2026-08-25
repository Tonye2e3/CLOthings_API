using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using CLOthings_API.DTOs.GroupShop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

// 這支是「假的第三方金流商」：模擬使用者在銀行/金流頁面付款的過程。
// 待付款資料先放在記憶體裡（不用另外開資料表），後端重啟會清空，但這時候本來就還沒有真正的訂單，
// 頂多是使用者要重新走一次結帳，不會弄丟已經成立的訂單。
// LINE Pay 那組端點是真的接 LINE Pay Sandbox，跟上面的模擬付款是兩條平行的路，
// 前端結帳頁選「LINE Pay」才會走 LINE Pay 這條，其他付款方式維持原本的模擬付款流程。
[Route("api/GroupPayment")]
[ApiController]
[Authorize(Roles = "User,SuperAdmin")] // 整支都是買家結帳流程，需要先登入
public class GroupPaymentController : ControllerBase
{
    private readonly CLOthingsContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public GroupPaymentController(CLOthingsContext context, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
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
        // 建立付款當下算好的應付金額，一併鎖起來存住。
        // LINE Pay 規定 /confirm 的金額要跟 /request 當初送出去的金額完全一致，
        // 不能等使用者從 LINE Pay 付款頁回來後才「重新」算一次金額再拿去 confirm——
        // 如果這段等待期間購物車被改了、或團購門檻被別人的訂單推過去，兩次算出來的金額就會兜不起來，
        // 導致使用者明明已經付款成功，LINE Pay 的 confirm 卻會被拒絕。
        public int Amount { get; set; }
    }

    // POST: api/GroupPayment/create
    // 結帳頁按下「前往付款」時呼叫這支：不會直接建立訂單，只是先把收件資訊記下來，換一個 paymentId
    [HttpPost("create")]
    [EnableRateLimiting("group")] // 防止有人狂建立待付款單塞爆記憶體
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
        PendingPayments[paymentId] = new PendingPaymentEntry { UserId = userId, Dto = dto, Amount = amount };

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
            // 改用 CreatePayment 當初鎖住存好的金額，不要再重新計算——
            // 跟 LINE Pay 那邊的 ConfirmLinePay 保持同一套邏輯：使用者在這個「確認訂單」頁面
            // 停留期間，就算購物車內容或團購門檻變了，這裡顯示的金額也要跟最後 ConfirmPayment
            // 實際建立訂單時的金額一致，不能讓畫面顯示一個數字、卻用另一個數字建單
            Amount = entry.Amount,
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

    // ================= 以下是真的接 LINE Pay Sandbox 的部分 =================

    // POST: api/GroupPayment/linepay/request
    // 結帳頁選「LINE Pay」、按下「前往付款」時呼叫這支：
    // 先跟 CreatePayment 一樣把收件資訊記下來、換一個 paymentId，
    // 再呼叫 LINE Pay 的 Request API 換一個 LINE Pay 的付款頁網址，回傳給前端整頁導過去
    [HttpPost("linepay/request")]
    [EnableRateLimiting("group")] // 防止有人狂發 LINE Pay 請求，浪費第三方 API 額度
    public async Task<ActionResult<LinePayRequestResultDTO>> RequestLinePay(GroupCheckoutDTO dto)
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

        var orderedQtyMap = await GetOrderedQtyMapAsync();
        var tierMap = await _context.GroupDiscountStandard.ToListAsync();

        var priced = cartItems.Select(c =>
        {
            var (unitPrice, _) = GroupCartController.ComputeUnitPrice(c.GroupProduct, tierMap, orderedQtyMap, c.GroupProductId, c.Quantity);
            return new { Cart = c, UnitPrice = unitPrice };
        }).ToList();

        var subtotal = priced.Sum(p => p.UnitPrice * p.Cart.Quantity);
        var freight = subtotal >= 1000 ? 0 : 60;
        var amount = subtotal + freight;

        var paymentId = Guid.NewGuid().ToString("N");
        // 把這次 request 送出去的金額鎖起來存住，等使用者從 LINE Pay 付款頁回來 confirm 時直接複用，
        // 不要再重新計算一次（見 PendingPaymentEntry.Amount 的說明）
        PendingPayments[paymentId] = new PendingPaymentEntry { UserId = userId, Dto = dto, Amount = amount };

        // LINE Pay 規定 packages[].amount 要等於底下 products 的加總，這裡把購物車品項跟運費分開列成兩個 product
        var products = priced.Select(p => new
        {
            name = p.Cart.GroupProduct.ProductName,
            quantity = p.Cart.Quantity,
            price = p.UnitPrice
        }).ToList<object>();

        if (freight > 0)
        {
            products.Add(new { name = "運費", quantity = 1, price = freight });
        }

        var requestBody = new
        {
            amount,
            currency = "TWD",
            orderId = paymentId,
            packages = new[]
            {
                new
                {
                    id = "package-1",
                    amount,
                    products
                }
            },
            redirectUrls = new
            {
                confirmUrl = _configuration["LinePay:ConfirmUrl"],
                cancelUrl = _configuration["LinePay:CancelUrl"]
            }
        };

        var (success, resultJson) = await CallLinePayApiAsync("POST", "/v3/payments/request", requestBody);
        if (!success)
        {
            PendingPayments.TryRemove(paymentId, out _);
            return BadRequest("呼叫 LINE Pay 發生錯誤：" + resultJson);
        }

        using var doc = JsonDocument.Parse(resultJson);
        var root = doc.RootElement;
        var returnCode = root.GetProperty("returnCode").GetString();
        if (returnCode != "0000")
        {
            PendingPayments.TryRemove(paymentId, out _);
            var returnMessage = root.TryGetProperty("returnMessage", out var msgEl) ? msgEl.GetString() : "未知錯誤";
            return BadRequest($"LINE Pay 拒絕這筆付款請求：[{returnCode}] {returnMessage}");
        }

        var paymentUrl = root.GetProperty("info").GetProperty("paymentUrl").GetProperty("web").GetString();

        return Ok(new LinePayRequestResultDTO { PaymentUrl = paymentUrl });
    }

    // GET: api/GroupPayment/linepay/confirm
    // 使用者在 LINE Pay 頁面完成付款後，LINE Pay 會把瀏覽器導回這支，
    // 網址上會帶 transactionId（LINE Pay 那筆交易的序號）跟 orderId（就是我們自己的 paymentId）
    // 這支不能要求登入，因為是 LINE Pay 直接呼叫的，不是我們前端呼叫
    [HttpGet("linepay/confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmLinePay([FromQuery] string transactionId, [FromQuery] string orderId)
    {
        var frontendSuccessUrl = _configuration["LinePay:FrontendSuccessUrl"];
        var frontendFailUrl = _configuration["LinePay:FrontendCancelUrl"];

        if (string.IsNullOrWhiteSpace(transactionId) || string.IsNullOrWhiteSpace(orderId))
        {
            return Redirect($"{frontendFailUrl}?linepay=fail&reason=missing_params");
        }

        // orderId 就是我們自己的 paymentId，用它找回這筆付款當初記下的收件資訊
        if (!PendingPayments.TryGetValue(orderId, out var entry))
        {
            return Redirect($"{frontendFailUrl}?linepay=fail&reason=not_found");
        }

        // 直接用 request 當初鎖住的金額，不能重新計算——
        // 如果這段等待期間購物車變了或團購門檻被別人推過去，重新算出來的金額會跟 LINE Pay
        // 那邊實際請款/顯示給使用者看的金額對不上，導致這支 confirm 被 LINE Pay 拒絕
        var amount = entry.Amount;

        var confirmBody = new { amount, currency = "TWD" };
        var (success, resultJson) = await CallLinePayApiAsync("POST", $"/v3/payments/{transactionId}/confirm", confirmBody);
        if (!success)
        {
            return Redirect($"{frontendFailUrl}?linepay=fail&reason=api_error");
        }

        using var doc = JsonDocument.Parse(resultJson);
        var returnCode = doc.RootElement.GetProperty("returnCode").GetString();
        if (returnCode != "0000")
        {
            return Redirect($"{frontendFailUrl}?linepay=fail&reason={returnCode}");
        }

        // LINE Pay 那邊確認付款成功了，這裡才是「真的」建立訂單的地方
        PendingPayments.TryRemove(orderId, out _);
        var order = await CreateOrderFromCartAsync(entry.UserId, entry.Dto);
        if (order == null)
        {
            return Redirect($"{frontendFailUrl}?linepay=fail&reason=cart_empty");
        }

        return Redirect($"{frontendSuccessUrl}?linepay=success&orderId={order.GroupOrderId}");
    }

    // GET: api/GroupPayment/linepay/cancel
    // 使用者在 LINE Pay 頁面按取消，LINE Pay 會把瀏覽器導回這支
    // 什麼都不用做，購物車跟 PendingPayments 都保留著，讓使用者可以重新選擇付款方式
    [HttpGet("linepay/cancel")]
    [AllowAnonymous]
    public IActionResult CancelLinePay()
    {
        var frontendCancelUrl = _configuration["LinePay:FrontendCancelUrl"];
        return Redirect($"{frontendCancelUrl}?linepay=cancelled");
    }

    // 呼叫 LINE Pay API 的共用小工具：組出 HMAC 簽章、送出請求、回傳 (是否成功, 回應內容)
    private async Task<(bool success, string body)> CallLinePayApiAsync(string method, string uri, object requestBody)
    {
        var channelId = _configuration["LinePay:ChannelId"];
        var channelSecret = _configuration["LinePay:ChannelSecret"];
        var baseUrl = _configuration["LinePay:BaseUrl"];

        var bodyJson = method == "POST" ? JsonSerializer.Serialize(requestBody) : "";
        var nonce = Guid.NewGuid().ToString();

        // LINE Pay 簽章規則（官方文件）：
        // Signature = Base64(HMAC-SHA256(ChannelSecret, ChannelSecret + URI + RequestBody + Nonce))
        var signText = channelSecret + uri + bodyJson + nonce;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(channelSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signText));
        var signature = Convert.ToBase64String(hash);

        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(new HttpMethod(method), baseUrl + uri);
        request.Headers.Add("X-LINE-ChannelId", channelId);
        request.Headers.Add("X-LINE-Authorization-Nonce", nonce);
        request.Headers.Add("X-LINE-Authorization", signature);

        if (method == "POST")
        {
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        }

        try
        {
            var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            return (true, responseBody);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
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
