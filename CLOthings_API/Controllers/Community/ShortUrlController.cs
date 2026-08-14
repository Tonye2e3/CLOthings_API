using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CLOthings_API.Models;

[Route("api/[controller]")]
[ApiController]
public class ShortUrlController : ControllerBase
{
    private readonly CLOthingsContext _context;
    private readonly IConfiguration _config;

    public ShortUrlController(CLOthingsContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    // POST: api/ShortUrl
    // 前端分享面板打開時呼叫這支，帶 communityPostId 進來。
    // 如果這篇貼文已經產生過短碼，直接把原本那組回傳（不會每次分享都重新生一組新的）；
    // 沒有的話才產生一組新的、存進 PostShortUrl 表，再回傳。
    [HttpPost]
    public async Task<ActionResult<ShortUrlResponseDTO>> CreateOrGetShortUrl(ShortUrlRequestDTO dto)
    {
        var postExists = await _context.CommunityPost.AnyAsync(p => p.CommunityPostId == dto.CommunityPostId);
        if (!postExists)
        {
            return NotFound("找不到這篇貼文");
        }

        var existing = await _context.PostShortUrl
            .FirstOrDefaultAsync(s => s.CommunityPostId == dto.CommunityPostId);
        if (existing != null)
        {
            return new ShortUrlResponseDTO { ShortCode = existing.ShortCode };
        }

        // 產生一組還沒被用過的短碼。用 while 迴圈是因為隨機碰撞的機率極低但理論上還是可能發生，
        // 撞到的話就再生一組，直到抓到一組資料庫裡沒人用過的為止。
        string code;
        do
        {
            code = GenerateShortCode();
        } while (await _context.PostShortUrl.AnyAsync(s => s.ShortCode == code));

        var shortUrl = new PostShortUrl
        {
            CommunityPostId = dto.CommunityPostId,
            ShortCode = code,
            CreatedDate = DateTimeOffset.Now
        };
        _context.PostShortUrl.Add(shortUrl);
        await _context.SaveChangesAsync();

        return new ShortUrlResponseDTO { ShortCode = code };
    }

    // GET: /s/{code}
    // 注意這裡的路由是 "/s/{code}"，不是 "api/ShortUrl/xxx"——用開頭加 "/" 的絕對路徑寫法，
    // 蓋掉最上面 [Route("api/[controller]")] 幫整個 Controller 設的路徑前綴，
    // 這樣短網址才能長得夠短（例如 https://your-api-domain/s/aB3xQ9），
    // 而不是掛一長串 https://your-api-domain/api/ShortUrl/aB3xQ9。
    //
    // 這支不是給前端 Vue 打的 API，是給「瀏覽器直接打開短網址」時用的：
    // 使用者點下短網址 → 瀏覽器對後端這個網址發出請求 → 後端查出這組短碼對應哪篇貼文 →
    // 用 302 轉址（Redirect）把瀏覽器導去前端 Vue 那篇貼文真正的網址。
    // 這一步一定要後端做，前端 Vue（SPA）沒辦法讓「連結本身」在使用者還沒載入 App 之前
    // 就先查資料庫、決定要導去哪裡。
    [HttpGet("/s/{code}")]
    public async Task<IActionResult> RedirectShortUrl(string code)
    {
        var shortUrl = await _context.PostShortUrl
            .FirstOrDefaultAsync(s => s.ShortCode == code);

        // frontendBaseUrl：前端網站的網址。先讀 appsettings.json 的 Frontend:BaseUrl，
        // 沒設定的話用開發環境常見的 http://localhost:5173 頂著（跟 Program.cs 裡 CORS
        // 允許的網址一樣）。之後部署到正式環境時，記得在 appsettings.json（或
        // appsettings.Production.json）把這個值換成真正的網域。
        var frontendBaseUrl = _config["Frontend:BaseUrl"] ?? "http://localhost:5173";

        if (shortUrl == null)
        {
            // 短碼查無資料（例如網址打錯、或資料被刪了）：導回社群首頁，而不是顯示一個
            // 生硬的 404 錯誤頁面，使用者體感上比較不會覺得「壞掉了」。
            return Redirect($"{frontendBaseUrl}/community");
        }

        return Redirect($"{frontendBaseUrl}/community/post/{shortUrl.CommunityPostId}");
    }

    // GenerateShortCode：隨機產生一組 6 碼的短碼，只用大小寫英文字母＋數字（base62），
    // 不含容易看錯的符號，短網址讀起來、打起來才不會混淆。
    private static string GenerateShortCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = Random.Shared;
        var result = new char[6];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }
        return new string(result);
    }
}