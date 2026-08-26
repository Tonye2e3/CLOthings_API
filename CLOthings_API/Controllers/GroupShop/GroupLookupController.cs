using CLOthings_API.DTOs.GroupShop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// 分類（GroupProductCategory）跟供應商（GroupSupplier）的管理端 CRUD，
// 同時也是「新增/編輯商品」表單下拉選單的資料來源
[Route("api/GroupLookup")]
[ApiController]
[Authorize(Roles = "Admin,SuperAdmin")] // 分類/供應商管理僅限管理員
public class GroupLookupController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public GroupLookupController(CLOthingsContext context)
    {
        _context = context;
    }

    // ================= 分類 =================

    // GET: api/GroupLookup/categories
    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<GroupProductCategoryDTO>>> GetCategories()
    {
        var categories = await _context.GroupProductCategory
            .Select(c => new GroupProductCategoryDTO
            {
                GroupProductCategoryId = c.GroupProductCategoryId,
                CategoryName = c.CategoryName,
                Description = c.Description
            })
            .ToListAsync();

        return Ok(categories);
    }

    // POST: api/GroupLookup/categories
    [HttpPost("categories")]
    public async Task<ActionResult<GroupProductCategoryDTO>> CreateCategory(SaveGroupProductCategoryDTO dto)
    {
        var category = new GroupProductCategory
        {
            CategoryName = dto.CategoryName,
            Description = dto.Description
        };

        _context.GroupProductCategory.Add(category);
        await _context.SaveChangesAsync();

        return Ok(new GroupProductCategoryDTO
        {
            GroupProductCategoryId = category.GroupProductCategoryId,
            CategoryName = category.CategoryName,
            Description = category.Description
        });
    }

    // PUT: api/GroupLookup/categories/5
    [HttpPut("categories/{id}")]
    public async Task<IActionResult> UpdateCategory(int id, SaveGroupProductCategoryDTO dto)
    {
        var category = await _context.GroupProductCategory.FindAsync(id);
        if (category == null)
        {
            return NotFound();
        }

        category.CategoryName = dto.CategoryName;
        category.Description = dto.Description;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/GroupLookup/categories/5
    [HttpDelete("categories/{id}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.GroupProductCategory.FindAsync(id);
        if (category == null)
        {
            return NotFound();
        }

        var inUse = await _context.GroupProduct.AnyAsync(p => p.GroupProductCategoryId == id);
        if (inUse)
        {
            return BadRequest("這個分類已經有商品在使用，不能刪除");
        }

        _context.GroupProductCategory.Remove(category);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ================= 供應商 =================

    // GET: api/GroupLookup/suppliers
    [HttpGet("suppliers")]
    public async Task<ActionResult<IEnumerable<GroupSupplierDTO>>> GetSuppliers()
    {
        var suppliers = await _context.GroupSupplier
            .Select(s => new GroupSupplierDTO
            {
                GroupSupplierId = s.GroupSupplierId,
                SupplierName = s.SupplierName,
                ContactName = s.ContactName,
                ContactTitle = s.ContactTitle,
                Address = s.Address,
                Phone = s.Phone
            })
            .ToListAsync();

        return Ok(suppliers);
    }

    // POST: api/GroupLookup/suppliers
    [HttpPost("suppliers")]
    public async Task<ActionResult<GroupSupplierDTO>> CreateSupplier(SaveGroupSupplierDTO dto)
    {
        var supplier = new GroupSupplier
        {
            SupplierName = dto.SupplierName,
            ContactName = dto.ContactName,
            ContactTitle = dto.ContactTitle,
            Address = dto.Address,
            Phone = dto.Phone
        };

        _context.GroupSupplier.Add(supplier);
        await _context.SaveChangesAsync();

        return Ok(new GroupSupplierDTO
        {
            GroupSupplierId = supplier.GroupSupplierId,
            SupplierName = supplier.SupplierName,
            ContactName = supplier.ContactName,
            ContactTitle = supplier.ContactTitle,
            Address = supplier.Address,
            Phone = supplier.Phone
        });
    }

    // PUT: api/GroupLookup/suppliers/5
    [HttpPut("suppliers/{id}")]
    public async Task<IActionResult> UpdateSupplier(int id, SaveGroupSupplierDTO dto)
    {
        var supplier = await _context.GroupSupplier.FindAsync(id);
        if (supplier == null)
        {
            return NotFound();
        }

        supplier.SupplierName = dto.SupplierName;
        supplier.ContactName = dto.ContactName;
        supplier.ContactTitle = dto.ContactTitle;
        supplier.Address = dto.Address;
        supplier.Phone = dto.Phone;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/GroupLookup/suppliers/5
    [HttpDelete("suppliers/{id}")]
    public async Task<IActionResult> DeleteSupplier(int id)
    {
        var supplier = await _context.GroupSupplier.FindAsync(id);
        if (supplier == null)
        {
            return NotFound();
        }

        var inUse = await _context.GroupProduct.AnyAsync(p => p.GroupSupplierId == id);
        if (inUse)
        {
            return BadRequest("這個供應商已經有商品在使用，不能刪除");
        }

        _context.GroupSupplier.Remove(supplier);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
