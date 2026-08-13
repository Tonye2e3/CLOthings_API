using CLOthings_API.DTOs.GroupShop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("api/GroupShipper")]
[ApiController]
[Authorize(Roles = "Admin,SuperAdmin")] // 物流商管理僅限管理員
public class GroupShipperController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public GroupShipperController(CLOthingsContext context)
    {
        _context = context;
    }

    // GET: api/GroupShipper
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GroupShipperDTO>>> GetShippers()
    {
        var shippers = await _context.GroupShipper
            .Select(s => new GroupShipperDTO
            {
                GroupShipperId = s.GroupShipperId,
                ShipperName = s.ShipperName,
                Email = s.Email,
                Address = s.Address
            })
            .ToListAsync();

        return Ok(shippers);
    }

    // POST: api/GroupShipper
    [HttpPost]
    public async Task<ActionResult<GroupShipperDTO>> CreateShipper(SaveGroupShipperDTO dto)
    {
        var shipper = new GroupShipper
        {
            ShipperName = dto.ShipperName,
            Email = dto.Email,
            Address = dto.Address
        };

        _context.GroupShipper.Add(shipper);
        await _context.SaveChangesAsync();

        return Ok(new GroupShipperDTO
        {
            GroupShipperId = shipper.GroupShipperId,
            ShipperName = shipper.ShipperName,
            Email = shipper.Email,
            Address = shipper.Address
        });
    }

    // PUT: api/GroupShipper/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateShipper(int id, SaveGroupShipperDTO dto)
    {
        var shipper = await _context.GroupShipper.FindAsync(id);
        if (shipper == null)
        {
            return NotFound();
        }

        shipper.ShipperName = dto.ShipperName;
        shipper.Email = dto.Email;
        shipper.Address = dto.Address;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/GroupShipper/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteShipper(int id)
    {
        var shipper = await _context.GroupShipper.FindAsync(id);
        if (shipper == null)
        {
            return NotFound();
        }

        var inUse = await _context.GroupOrder.AnyAsync(o => o.GroupShipperId == id);
        if (inUse)
        {
            return BadRequest("這個物流商已經有訂單在使用，不能刪除");
        }

        _context.GroupShipper.Remove(shipper);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
