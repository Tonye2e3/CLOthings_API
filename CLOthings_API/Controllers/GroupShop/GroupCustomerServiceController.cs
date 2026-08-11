using CLOthings_API.DTOs.GroupShop;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("api/GroupCustomerService")]
[ApiController]
public class GroupCustomerServiceController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public GroupCustomerServiceController(CLOthingsContext context)
    {
        _context = context;
    }

    // POST: api/GroupCustomerService/order/5   (5 是 GroupOrderId)
    // 買家針對某一筆訂單提出問題
    [HttpPost("order/{orderId}")]
    public async Task<ActionResult<GroupCustomerServiceDTO>> Create(int orderId, CreateGroupCustomerServiceDTO dto)
    {
        var orderExists = await _context.GroupOrder.AnyAsync(o => o.GroupOrderId == orderId);
        if (!orderExists)
        {
            return NotFound("找不到這筆訂單");
        }

        if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Content))
        {
            return BadRequest("請填寫標題與內容");
        }

        var record = new GroupCustomerService
        {
            GroupOrderId = orderId,
            Name = dto.Name,
            Email = dto.Email,
            Phone = dto.Phone,
            Title = dto.Title,
            Content = dto.Content
        };

        _context.GroupCustomerService.Add(record);
        await _context.SaveChangesAsync();

        return Ok(ToDTO(record));
    }

    // GET: api/GroupCustomerService/order/5   (5 是 GroupOrderId)
    // 買家查詢自己針對這筆訂單送出過的客服紀錄
    [HttpGet("order/{orderId}")]
    public async Task<ActionResult<IEnumerable<GroupCustomerServiceDTO>>> GetByOrder(int orderId)
    {
        var records = await _context.GroupCustomerService
            .Where(c => c.GroupOrderId == orderId)
            .ToListAsync();

        return Ok(records.Select(ToDTO).ToList());
    }

    // GET: api/GroupCustomerService/admin/all
    // 管理端查詢全部客服紀錄
    [HttpGet("admin/all")]
    public async Task<ActionResult<IEnumerable<GroupCustomerServiceDTO>>> GetAll()
    {
        var records = await _context.GroupCustomerService.ToListAsync();
        return Ok(records.Select(ToDTO).ToList());
    }

    private static GroupCustomerServiceDTO ToDTO(GroupCustomerService c)
    {
        return new GroupCustomerServiceDTO
        {
            GroupCustomerServiceId = c.GroupCustomerServiceId,
            GroupOrderId = c.GroupOrderId,
            Name = c.Name,
            Email = c.Email,
            Phone = c.Phone,
            Title = c.Title,
            Content = c.Content
        };
    }
}
