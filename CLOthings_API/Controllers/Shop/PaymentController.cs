using CLOthings_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CLOthings_API.Controllers.Shop
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PaymentController : ControllerBase
    {
        private readonly CLOthingsContext _context;
        private readonly IConfiguration _config;

        public PaymentController(CLOthingsContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // POST api/payment/{orderId} —— 產生綠界付款表單
        [HttpPost("{orderId}")]
        public async Task<IActionResult> CreatePayment(int orderId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // 找訂單（含明細，算金額用），且必須是自己的
            var order = await _context.Order
                .Include(o => o.OrderDetail)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.UserId == userId);

            if (order == null) return NotFound(new { message = "找不到訂單" });

            // 算總金額
            int totalAmount = (int)order.OrderDetail.Sum(d => d.Price * d.Quantity);

            // 綠界設定
            var merchantID = _config["ECPay:MerchantID"];
            var hashKey = _config["ECPay:HashKey"];
            var hashIV = _config["ECPay:HashIV"];
            var paymentUrl = _config["ECPay:PaymentUrl"];

            // 組付款參數
            var parameters = new SortedDictionary<string, string>
            {
                { "MerchantID", merchantID },
                { "MerchantTradeNo", "CLO" + DateTime.Now.ToString("yyyyMMddHHmmss") + orderId },
                { "MerchantTradeDate", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") },
                { "PaymentType", "aio" },
                { "TotalAmount", totalAmount.ToString() },
                { "TradeDesc", "CLOthings Order" },
                { "ItemName", "CLOthings 商品訂單" },
                { "ReturnURL", "https://finite-murkiness-untimely.ngrok-free.dev/api/payment/notify" },// 之後改 ngrok
                { "ChoosePayment", "ALL" },
                { "EncryptType", "1" },
                { "CustomField1", orderId.ToString() },
            };

            // 算檢查碼
            parameters.Add("CheckMacValue", GetCheckMacValue(parameters, hashKey, hashIV));

            // 組成自動送出的 HTML form
            var sb = new StringBuilder();
            sb.Append($"<form id='ecpay' method='post' action='{paymentUrl}'>");
            foreach (var p in parameters)
            {
                sb.Append($"<input type='hidden' name='{p.Key}' value='{p.Value}' />");
            }
            sb.Append("</form><script>document.getElementById('ecpay').submit();</script>");

            return Content(sb.ToString(), "text/html");
        }

        // 產生 CheckMacValue（綠界固定規則）
        private string GetCheckMacValue(SortedDictionary<string, string> parameters, string hashKey, string hashIV)
        {
            // 1. 參數已排序，串接
            var raw = $"HashKey={hashKey}&" + string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}")) + $"&HashIV={hashIV}";
            // 2. URL Encode + 小寫
            raw = System.Web.HttpUtility.UrlEncode(raw).ToLower();
            // 3. 綠界的編碼替換規則
            raw = raw.Replace("%2d", "-").Replace("%5f", "_").Replace("%2e", ".").Replace("%21", "!")
                     .Replace("%2a", "*").Replace("%28", "(").Replace("%29", ")");
            // 4. SHA256
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return BitConverter.ToString(hash).Replace("-", "").ToUpper();
        }

        // POST api/payment/notify —— 接收綠界付款結果通知
        [HttpPost("notify")]
        [AllowAnonymous]   // 綠界機器呼叫，沒有 token
        public async Task<IActionResult> Notify([FromForm] IFormCollection form)
        {
            var hashKey = _config["ECPay:HashKey"];
            var hashIV = _config["ECPay:HashIV"];

            // 把綠界送來的參數收進來（除了 CheckMacValue）
            var parameters = new SortedDictionary<string, string>();
            foreach (var key in form.Keys)
            {
                if (key != "CheckMacValue")
                    parameters.Add(key, form[key]);
            }

            // 自己算一次檢查碼，跟綠界送來的比對
            var myCheckMac = GetCheckMacValue(parameters, hashKey, hashIV);
            var ecpayCheckMac = form["CheckMacValue"].ToString();

            if (myCheckMac != ecpayCheckMac)
            {
                return Content("0|CheckMacValue Error");   // 驗證失敗
            }

            // 驗證通過 → 看付款結果
            var rtnCode = form["RtnCode"].ToString();   // 1 = 付款成功
            var merchantTradeNo = form["MerchantTradeNo"].ToString();

            if (rtnCode == "1")
            {
                var orderId = int.Parse(form["CustomField1"].ToString());
                var order = await _context.Order.FindAsync(orderId);
                if (order != null && order.Status == "待付款")
                {
                    order.Status = "待出貨";   // 付款成功 → 待出貨
                    await _context.SaveChangesAsync();
                }
            }

            return Content("1|OK");   // 一定要回這個給綠界
        }

    }


}