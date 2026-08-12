using CLOthings_API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CLOthings_API.Controllers.Shop
{
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

        // GET api/product
        [HttpGet] //這個方法對應GET請求
        public async Task<IActionResult> GetProducts() { 
            //從DB撈所有商品
            var products = await _context.Product.ToListAsync();

            //回傳商品清單，並自動轉JSON
            return Ok(products);
        }




    }
}
