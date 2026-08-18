using CLOthings_API.DTOs.GroupShop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Authorization;
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
    // 買家端一律只回傳「上架中」的商品，已下架商品（GroupProductAdminView 刪除已有訂單的商品時
    // 會把 Status 改成「已下架」，而不是真的刪除，用來保留歷史訂單資料）不應該再讓買家看到、買到
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GroupProductDTO>>> GetGroupProducts([FromQuery] string keyword = null)
    {
        var query = _context.GroupProduct.Where(p => p.Status == "上架中");

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
    // 買家端商品詳情：同樣只允許看「上架中」的商品，已下架商品直接當成 404，
    // 避免有人用舊的分享連結、或直接改網址上的 id，繞過商品列表頁看到已下架商品
    [HttpGet("{id}")]
    public async Task<ActionResult<GroupProductDTO>> GetGroupProduct(int id)
    {
        var product = await _context.GroupProduct.FindAsync(id);
        if (product == null || product.Status != "上架中")
        {
            return NotFound();
        }

        // 埋點：每次有人打開商品詳情頁，就把今天這個商品的瀏覽次數 +1
        await IncrementStatAsync(id, s => s.ViewCount = (s.ViewCount ?? 0) + 1);

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
            if (product == null) return; // 商品不存在就不用記統計了

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

    // ================= 以下是管理端（商品上架/編輯/下架 + 階層 + 規格）的 API =================

    // POST: api/GroupProduct/upload-image
    // 真正的圖片上傳：把管理員選的商品圖片存進 wwwroot/images/group-products/，回傳存好之後的路徑。
    // 前端流程是「先呼叫這支把圖片存好、拿到路徑」，再把這個路徑存進 SaveGroupProductDTO.ProductImg，
    // 這支本身不會動 GroupProduct 這張表。做法跟 CommunityPostController.UploadImages 一致。
    [HttpPost("upload-image")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<string>> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("請選擇圖片檔案");
        }

        var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "group-products");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        // 檔名用 Guid.NewGuid() 重新命名，避免不同管理員剛好選了同名的檔案互相覆蓋掉對方的圖片
        var extension = Path.GetExtension(file.FileName);
        var newFileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(folder, newFileName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // 回傳的是「相對路徑」（例如 /images/group-products/xxx.jpg），前端組網址時直接接在 API_BASE 後面即可
        return Ok($"/images/group-products/{newFileName}");
    }

    // GET: api/GroupProduct/5/edit
    // 編輯商品表單要用的原始欄位（GroupSupplierId / GroupProductCategoryId 這些買家端的 DTO 不會回傳）
    [HttpGet("{id}/edit")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<SaveGroupProductDTO>> GetProductForEdit(int id)
    {
        var product = await _context.GroupProduct.FindAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        return Ok(new SaveGroupProductDTO
        {
            ProductName = product.ProductName,
            GroupSupplierId = product.GroupSupplierId,
            GroupProductCategoryId = product.GroupProductCategoryId,
            Description = product.Description,
            Price = (int)product.Price,
            Status = product.Status,
            ProductImg = product.ProductImg,
            SalesStart = product.SalesStart,
            SalesEnd = product.SalesEnd
        });
    }

    // POST: api/GroupProduct
    // 新增團購商品
    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<GroupProductDTO>> CreateProduct(SaveGroupProductDTO dto)
    {
        var product = new GroupProduct
        {
            ProductName = dto.ProductName,
            GroupSupplierId = dto.GroupSupplierId,
            GroupProductCategoryId = dto.GroupProductCategoryId,
            Description = dto.Description,
            Price = dto.Price,
            Status = dto.Status,
            ProductImg = dto.ProductImg,
            SalesStart = dto.SalesStart,
            SalesEnd = dto.SalesEnd
        };

        _context.GroupProduct.Add(product);
        await _context.SaveChangesAsync();

        return Ok(BuildProductDTO(product, new Dictionary<int, int>(), new List<GroupDiscountStandard>()));
    }

    // PUT: api/GroupProduct/5
    // 編輯團購商品基本資料
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> UpdateProduct(int id, SaveGroupProductDTO dto)
    {
        var product = await _context.GroupProduct.FindAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        product.ProductName = dto.ProductName;
        product.GroupSupplierId = dto.GroupSupplierId;
        product.GroupProductCategoryId = dto.GroupProductCategoryId;
        product.Description = dto.Description;
        product.Price = dto.Price;
        product.Status = dto.Status;
        product.ProductImg = dto.ProductImg;
        product.SalesStart = dto.SalesStart;
        product.SalesEnd = dto.SalesEnd;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/GroupProduct/5
    // 下架/刪除商品：如果已經有訂單明細用到這個商品，改成把 Status 設為「已下架」，避免刪掉造成訂單資料出錯；
    // 完全沒有任何訂單用過的商品才會真的整筆刪除
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await _context.GroupProduct.FindAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        var hasOrders = await _context.GroupOrderDetail.AnyAsync(d => d.GroupProductId == id);
        if (hasOrders)
        {
            product.Status = "已下架";
            await _context.SaveChangesAsync();
            return Ok(new { message = "此商品已經有訂單資料，改為下架而非刪除" });
        }

        var specs = await _context.GroupProductSpecification.Where(s => s.GroupProductId == id).ToListAsync();
        var tiers = await _context.GroupDiscountStandard.Where(t => t.GroupProductId == id).ToListAsync();
        _context.GroupProductSpecification.RemoveRange(specs);
        _context.GroupDiscountStandard.RemoveRange(tiers);
        _context.GroupProduct.Remove(product);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // ---- 團購階層（GroupDiscountStandard） ----

    // GET: api/GroupProduct/5/tiers
    [HttpGet("{id}/tiers")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<IEnumerable<TierAdminDTO>>> GetTiers(int id)
    {
        // 注意：要先 ToListAsync() 把資料抓回記憶體，才能在 Select() 裡呼叫 ParseThresholdCount()。
        // 原本寫法是整條 LINQ 鏈（Where→Select→OrderBy→ToListAsync）都還是 IQueryable，
        // EF Core 會試著把 Select 裡的 ParseThresholdCount(...) 翻譯成 SQL，
        // 但它是一般 C# 方法、無法翻譯，因此不管有沒有資料都會直接丟例外、回傳 500。
        var rawTiers = await _context.GroupDiscountStandard
            .Where(t => t.GroupProductId == id)
            .ToListAsync();

        var tiers = rawTiers
            .Select(t => new TierAdminDTO
            {
                GroupDiscountStandardId = t.GroupDiscountStandardId,
                TierLevel = t.TierLevel,
                ThresholdCount = ParseThresholdCount(t.ThresholdCount),
                DiscountRate = t.DiscountRate ?? 1m
            })
            .OrderBy(t => t.ThresholdCount)
            .ToList();

        return Ok(tiers);
    }

    // POST: api/GroupProduct/5/tiers
    [HttpPost("{id}/tiers")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<TierAdminDTO>> AddTier(int id, SaveTierDTO dto)
    {
        var product = await _context.GroupProduct.FindAsync(id);
        if (product == null)
        {
            return NotFound("找不到這個商品");
        }

        var tier = new GroupDiscountStandard
        {
            GroupProductId = id,
            TierLevel = dto.TierLevel,
            ThresholdCount = dto.ThresholdCount.ToString(),
            DiscountRate = dto.DiscountRate
        };

        _context.GroupDiscountStandard.Add(tier);
        await _context.SaveChangesAsync();

        return Ok(new TierAdminDTO
        {
            GroupDiscountStandardId = tier.GroupDiscountStandardId,
            TierLevel = tier.TierLevel,
            ThresholdCount = dto.ThresholdCount,
            DiscountRate = dto.DiscountRate
        });
    }

    // PUT: api/GroupProduct/tiers/5   (5 是 GroupDiscountStandardId)
    [HttpPut("tiers/{tierId}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> UpdateTier(int tierId, SaveTierDTO dto)
    {
        var tier = await _context.GroupDiscountStandard.FindAsync(tierId);
        if (tier == null)
        {
            return NotFound();
        }

        tier.TierLevel = dto.TierLevel;
        tier.ThresholdCount = dto.ThresholdCount.ToString();
        tier.DiscountRate = dto.DiscountRate;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/GroupProduct/tiers/5
    [HttpDelete("tiers/{tierId}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> DeleteTier(int tierId)
    {
        var tier = await _context.GroupDiscountStandard.FindAsync(tierId);
        if (tier == null)
        {
            return NotFound();
        }

        _context.GroupDiscountStandard.Remove(tier);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ---- 商品規格（GroupProductSpecification，尺寸/顏色） ----

    // GET: api/GroupProduct/5/specifications
    [HttpGet("{id}/specifications")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<IEnumerable<SpecAdminDTO>>> GetSpecifications(int id)
    {
        var specs = await _context.GroupProductSpecification
            .Where(s => s.GroupProductId == id)
            .Select(s => new SpecAdminDTO
            {
                GroupProductSpecificationId = s.GroupProductSpecificationId,
                Size = s.Size,
                Color = s.Color
            })
            .ToListAsync();

        return Ok(specs);
    }

    // POST: api/GroupProduct/5/specifications
    [HttpPost("{id}/specifications")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<SpecAdminDTO>> AddSpecification(int id, SaveSpecDTO dto)
    {
        var product = await _context.GroupProduct.FindAsync(id);
        if (product == null)
        {
            return NotFound("找不到這個商品");
        }

        var spec = new GroupProductSpecification
        {
            GroupProductId = id,
            Size = dto.Size,
            Color = dto.Color
        };

        _context.GroupProductSpecification.Add(spec);
        await _context.SaveChangesAsync();

        return Ok(new SpecAdminDTO
        {
            GroupProductSpecificationId = spec.GroupProductSpecificationId,
            Size = spec.Size,
            Color = spec.Color
        });
    }

    // DELETE: api/GroupProduct/specifications/5
    [HttpDelete("specifications/{specId}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> DeleteSpecification(int specId)
    {
        var spec = await _context.GroupProductSpecification.FindAsync(specId);
        if (spec == null)
        {
            return NotFound();
        }

        var inUse = await _context.GroupCart.AnyAsync(c => c.GroupProductSpecificationId == specId)
            || await _context.GroupOrderDetail.AnyAsync(d => d.GroupProductSpecificationId == specId);

        if (inUse)
        {
            return BadRequest("這個規格已經被購物車或訂單使用過，不能刪除");
        }

        _context.GroupProductSpecification.Remove(spec);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}