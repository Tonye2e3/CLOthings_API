using CLOthings_API.DTOs.GroupShop;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// 7-11（統一超商）電子地圖串接：讓買家在結帳頁選超商門市，取得「門市代號」。
// 電子地圖本身不用簽章/加密，前端可以直接組網址導轉過去，
// 這支只需要留「接收超商回傳門市資料」這個 callback——因為超商是用瀏覽器表單 POST 回來，
// 前端 SPA 沒辦法直接接收 POST body，一定要有後端網址去接。
[Route("api/GroupLogistics")]
[ApiController]
public class GroupLogisticsController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public GroupLogisticsController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // POST: api/GroupLogistics/cvs-map/callback
    // 買家在超商電子地圖選完門市後，超商系統會把瀏覽器導回這支、並用表單 POST 方式帶回門市資料
    // 這支不需要（也不能要求）登入，因為是超商系統直接呼叫的，不是我們的前端呼叫
    [HttpPost("cvs-map/callback")]
    [AllowAnonymous]
    public IActionResult CvsMapCallback([FromForm] CvsMapCallbackDTO dto)
    {
        var frontendUrl = _configuration["SevenElevenMap:FrontendReturnUrl"];

        // 把選好的門市資料用 query string 帶回前端結帳頁，前端進頁面時讀出來顯示
        var query = $"?cvsStoreId={Uri.EscapeDataString(dto.StoreID ?? "")}" +
                    $"&cvsStoreName={Uri.EscapeDataString(dto.StoreName ?? "")}" +
                    $"&cvsAddress={Uri.EscapeDataString(dto.Address ?? "")}";

        return Redirect(frontendUrl + query);
    }
}
