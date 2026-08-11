using CLOthings_API.DTOs.GroupShop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// 給「新增/編輯商品」表單用的下拉選單資料，唯讀，分類跟供應商本身的管理不在這支處理
[Route("api/GroupLookup")]
[ApiController]
public class GroupLookupController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public GroupLookupController(CLOthingsContext context)
    {
        _context = context;
    }

    // GET: api/GroupLookup/categories
    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<GroupProductCategoryDTO>>> GetCategories()
    {
        var categories = await _context.GroupProductCategory
            .Select(c => new GroupProductCategoryDTO { GroupProductCategoryId = c.GroupProductCategoryId, CategoryName = c.CategoryName })
            .ToListAsync();

        return Ok(categories);
    }

    // GET: api/GroupLookup/suppliers
    [HttpGet("suppliers")]
    public async Task<ActionResult<IEnumerable<GroupSupplierDTO>>> GetSuppliers()
    {
        var suppliers = await _context.GroupSupplier
            .Select(s => new GroupSupplierDTO { GroupSupplierId = s.GroupSupplierId, SupplierName = s.SupplierName })
            .ToListAsync();

        return Ok(suppliers);
    }
}
