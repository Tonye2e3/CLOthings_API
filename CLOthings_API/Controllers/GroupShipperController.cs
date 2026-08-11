using CLOthings_API.DTOs.GroupShop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("api/GroupShipper")]
[ApiController]
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
}
