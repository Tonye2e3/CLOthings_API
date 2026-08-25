using CLOthings_API.DTOs;
using CLOthings_API.DTOs.Shop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CLOthings_API.Controllers.Shop
{
    //<h1>2026/08/25 13:41</h1>
    [Route("api/[controller]")]   //決定API網址，[Ctr]自動代換成控制器名稱
    [ApiController]               //告訴ASP.NET這是一個ASP Ctr
    public class ProductController : ControllerBase //繼承ControllerBase，會有OK()NotFound()可用
    {
        // 宣告一個欄位，用來存資料庫連線
        private readonly CLOthingsContext _context;

        // 建構子：ASP.NET會自動把DbContext注入進來
        public ProductController(CLOthingsContext context) { 
            _context = context;
        }

        // 撈商品頁面=>對應ShopView.vue
        // GET api/product
        [HttpGet] //這個方法對應GET請求
        public async Task<IActionResult> GetProducts() { 
            //從DB撈所有商品
            var products = await _context.Product
                .Include(p=>p.ProductImg)
                .Select(p=> new ProductDto {
                    ProductId = p.ProductId,
                    ProductName= p.ProductName,
                    Price = p.Price,
                    Description = p.Description,
                    Status = p.Status,
                    ProductImgFile=p.ProductImg.Select(img=> img.ProductImgFile).FirstOrDefault(),
                    ProductCategoryId = p.ProductCategoryId,
                })
                .ToListAsync();

            //回傳商品清單，並自動轉JSON
            return Ok(products);
        }

        // 撈商品詳情頁面=>對應ProductView.vue
        // GET api/product/id
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProduct(int id)
        {
            var product = await _context.Product
                .Include(p => p.ProductImg)              // 撈圖片
                .Include(p => p.ProductSpecification)    // 撈規格
                .Where(p => p.ProductId == id)           // 只要這個 id
                .Select(p => new ProductDetailDto
                {
                    ProductId = p.ProductId,
                    ProductName = p.ProductName,
                    Price = p.Price,
                    Description = p.Description,
                    // 把規格表的每一筆，轉成 ProductSpecDto
                    Specifications = p.ProductSpecification.Select(s => new ProductDetailSpecDto
                    {
                        ProductSpecificationId = s.ProductSpecificationId,
                        Color = s.Color,
                        Size = s.Size,
                        Inventory = s.Inventory,
                    }).ToList(),
                    // 把圖片表的每一筆，轉成檔名清單
                    Images = p.ProductImg.Select(img => img.ProductImgFile).ToList(),
                })
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return NotFound();   // 找不到這個商品
            }

            return Ok(product);
        }

        // 關鍵字搜尋
        // GET api/product/search?keyword=xxx
        [HttpGet("search")]
        public async Task<IActionResult> Search(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return Ok(new List<ProductDto>());
            }

            var products = await _context.Product
                .Where(p => p.ProductName.Contains(keyword))
                .Include(p => p.ProductImg)
                .Select(p => new ProductDto
                {
                    ProductId = p.ProductId,
                    ProductName = p.ProductName,
                    Price = p.Price,
                    Description = p.Description,
                    Status = p.Status,
                    ProductImgFile = p.ProductImg.Select(img => img.ProductImgFile).FirstOrDefault(),
                    ProductCategoryId = p.ProductCategoryId,
                })
                .ToListAsync();

            return Ok(products);
        }


    }
}
