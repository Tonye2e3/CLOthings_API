using Microsoft.AspNetCore.Mvc;

namespace CLOthings_API.Controllers.Diagnostics
{
    // 這支 Controller 純粹是為了「展示Group這部分的負載均衡有沒有生效」而加的，跟商品/購物車等商業邏輯無關。
    // 路由刻意取名 api/GroupWhoAmI（Group開頭），這樣nginx才會把它歸進Group的負載均衡pool做測試，
    // 不會跟User/Shop/Community那些組員負責的路由混在一起。
    // 不加 [Authorize]，這樣demo時不用先登入就能直接在瀏覽器打開網址看結果。
    [Route("api/GroupWhoAmI")]
    [ApiController]
    public class GroupWhoAmIController : ControllerBase
    {
        // GET: api/GroupWhoAmI
        // 回傳目前是「哪一個 dotnet run 實例（哪個 port）」在處理這個請求。
        // Demo流程：開3個實例（lb1/lb2/lb3），透過 Nginx 打這支API，
        // 重整頁面幾次，觀察 port 有沒有在 5301 / 5302 / 5303 之間輪流變化。
        [HttpGet]
        public IActionResult Get()
        {
            var port = HttpContext.Connection.LocalPort;
            var machineName = Environment.MachineName;
            var processId = Environment.ProcessId;

            return Ok(new
            {
                message = $"這個請求是由 port {port} 的伺服器處理的",
                port,
                machineName,
                processId,
                time = DateTimeOffset.Now
            });
        }
    }
}
