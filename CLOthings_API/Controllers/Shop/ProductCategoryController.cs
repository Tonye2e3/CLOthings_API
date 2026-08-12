using CLOthings_API.DTOs.Shop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CLOthings_API.Controllers.Shop
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductCategoryController : ControllerBase
    {
        private readonly CLOthingsContext _context;
        public ProductCategoryController(CLOthingsContext context)
        {
            _context = context;
        }

        // Get api/productCategory
        [HttpGet]
        public async Task<IActionResult> GetProductCategory() {
            var productCategory = await _context.ProductCategory
                .Select(p=> new ProductCategoryDto {
                    ProductCategoryId =p.ProductCategoryId,
                    CategoryName =p.CategoryName,
            })
                .ToListAsync();
            return Ok(productCategory);
        }

    }
}
