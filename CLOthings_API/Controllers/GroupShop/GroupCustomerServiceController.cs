//using System.Security.Claims;
//using CLOthings_API.DTOs.GroupShop;
//using CLOthings_API.Models;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;

//[Route("api/GroupCustomerService")]
//[ApiController]
//public class GroupCustomerServiceController : ControllerBase
//{
//    private readonly CLOthingsContext _context;
//    public GroupCustomerServiceController(CLOthingsContext context)
//    {
//        _context = context;
//    }

//    // 從 JWT 的 Claims 取得目前登入者的 UserId，不再讓前端（Vue）自己傳 UserId 過來
//    // 這個方法只能在已經掛 [Authorize] 的 Controller/Action 裡呼叫，否則 Claims 裡不會有這筆資料
//    private int GetUserId()
//    {
//        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
//        return int.TryParse(value, out var id) ? id : 0;
//    }

//    // POST: api/GroupCustomerService/order/5   (5 是 GroupOrderId)
//    // 買家針對某一筆訂單提出問題
//    [HttpPost("order/{orderId}")]
//    [Authorize(Roles = "User,SuperAdmin")]
//    public async Task<ActionResult<GroupCustomerServiceDTO>> Create(int orderId, CreateGroupCustomerServiceDTO dto)
//    {
//        var order = await _context.GroupOrder.FindAsync(orderId);
//        if (order == null)
//        {
//            return NotFound("找不到這筆訂單");
//        }

//        // 只能對自己的訂單提出客服問題，SuperAdmin 不受限
//        if (order.UserId != GetUserId() && !User.IsInRole("SuperAdmin"))
//        {
//            return Forbid();
//        }

//        if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Content))
//        {
//            return BadRequest("請填寫標題與內容");
//        }

//        var record = new GroupCustomerService
//        {
//            GroupOrderId = orderId,
//            Name = dto.Name,
//            Email = dto.Email,
//            Phone = dto.Phone,
//            Title = dto.Title,
//            Content = dto.Content
//        };

//        _context.GroupCustomerService.Add(record);
//        await _context.SaveChangesAsync();

//        return Ok(ToDTO(record));
//    }

//    // GET: api/GroupCustomerService/order/5   (5 是 GroupOrderId)
//    // 買家查詢自己針對這筆訂單送出過的客服紀錄
//    [HttpGet("order/{orderId}")]
//    [Authorize(Roles = "User,SuperAdmin")]
//    public async Task<ActionResult<IEnumerable<GroupCustomerServiceDTO>>> GetByOrder(int orderId)
//    {
//        var order = await _context.GroupOrder.FindAsync(orderId);
//        if (order == null)
//        {
//            return NotFound("找不到這筆訂單");
//        }

//        // 只能查自己訂單的客服紀錄，避免改網址上的 orderId 就能看到別人的聯絡資料與內容，SuperAdmin 不受限
//        if (order.UserId != GetUserId() && !User.IsInRole("SuperAdmin"))
//        {
//            return Forbid();
//        }

//        var records = await _context.GroupCustomerService
//            .Where(c => c.GroupOrderId == orderId)
//            .ToListAsync();

//        return Ok(records.Select(ToDTO).ToList());
//    }

//    // GET: api/GroupCustomerService/admin/all
//    // 管理端查詢全部客服紀錄
//    [HttpGet("admin/all")]
//    [Authorize(Roles = "Admin,SuperAdmin")]
//    public async Task<ActionResult<IEnumerable<GroupCustomerServiceDTO>>> GetAll()
//    {
//        var records = await _context.GroupCustomerService.ToListAsync();
//        return Ok(records.Select(ToDTO).ToList());
//    }

//    // PUT: api/GroupCustomerService/5/reply   (5 是 GroupCustomerServiceId)
//    // 管理端回覆某一筆客服紀錄
//    [HttpPut("{id}/reply")]
//    [Authorize(Roles = "Admin,SuperAdmin")]
//    public async Task<ActionResult<GroupCustomerServiceDTO>> Reply(int id, ReplyGroupCustomerServiceDTO dto)
//    {
//        if (string.IsNullOrWhiteSpace(dto.ReplyContent))
//        {
//            return BadRequest("請填寫回覆內容");
//        }

//        var record = await _context.GroupCustomerService.FindAsync(id);
//        if (record == null)
//        {
//            return NotFound();
//        }

//        record.ReplyContent = dto.ReplyContent;
//        record.RepliedAt = DateTimeOffset.Now;
//        await _context.SaveChangesAsync();

//        return Ok(ToDTO(record));
//    }

//    private static GroupCustomerServiceDTO ToDTO(GroupCustomerService c)
//    {
//        return new GroupCustomerServiceDTO
//        {
//            GroupCustomerServiceId = c.GroupCustomerServiceId,
//            GroupOrderId = c.GroupOrderId,
//            Name = c.Name,
//            Email = c.Email,
//            Phone = c.Phone,
//            Title = c.Title,
//            Content = c.Content,
//            ReplyContent = c.ReplyContent,
//            RepliedAt = c.RepliedAt.HasValue ? c.RepliedAt.Value.ToString("yyyy/MM/dd HH:mm") : null
//        };
//    }
//}
