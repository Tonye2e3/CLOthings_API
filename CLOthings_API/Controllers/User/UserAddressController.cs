using CLOthings_API.DTOs;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CLOthings_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserAddressController : ControllerBase
    {
        private readonly CLOthingsContext _context;

        public UserAddressController(CLOthingsContext context)
        {
            _context = context;
        }

        // 取得 JWT 裡目前登入會員的 UserId
        private bool TryGetCurrentUserId(out int userId)
        {
            var userIdString =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(userIdString, out userId);
        }


        // GET: api/UserAddress/me
        // 取得目前會員全部收件地址
        [HttpGet("me")]
        public async Task<ActionResult> GetMyAddresses()
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var addresses = await _context.UserAddress
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ThenBy(a => a.UserAddressId)
                .Select(a => new
                {
                    a.UserAddressId,
                    a.RecipientName,
                    a.RecipientPhone,
                    a.PostalCode,
                    a.AddressDetail,
                    a.IsDefault
                })
                .ToListAsync();

            return Ok(addresses);
        }


        // GET: api/UserAddress/me/default
        // 取得目前會員的預設地址
        [HttpGet("me/default")]
        public async Task<ActionResult> GetMyDefaultAddress()
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var address = await _context.UserAddress
                .Where(a =>
                    a.UserId == userId &&
                    a.IsDefault == true)
                .Select(a => new
                {
                    a.UserAddressId,
                    a.RecipientName,
                    a.RecipientPhone,
                    a.PostalCode,
                    a.AddressDetail,
                    a.IsDefault
                })
                .FirstOrDefaultAsync();

            if (address == null)
            {
                return NotFound("尚未設定預設地址");
            }

            return Ok(address);
        }


        // POST: api/UserAddress/me
        // 新增收件地址
        [HttpPost("me")]
        public async Task<ActionResult> CreateMyAddress(
            UserAddressDTO dto)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            // 最多 5 筆
            var addressCount = await _context.UserAddress
                .CountAsync(a => a.UserId == userId);

            if (addressCount >= 5)
            {
                return BadRequest("最多只能新增 5 筆收件地址");
            }

            // 第一筆地址自動成為預設地址
            var shouldBeDefault =
                addressCount == 0 || dto.IsDefault == true;

            // 如果新地址要成為預設
            // 先把其他地址取消預設
            if (shouldBeDefault)
            {
                var oldDefaults = await _context.UserAddress
                    .Where(a =>
                        a.UserId == userId &&
                        a.IsDefault == true)
                    .ToListAsync();

                foreach (var item in oldDefaults)
                {
                    item.IsDefault = false;
                }
            }

            var address = new UserAddress
            {
                UserId = userId,
                RecipientName = dto.RecipientName,
                RecipientPhone = dto.RecipientPhone,
                PostalCode = dto.PostalCode,
                AddressDetail = dto.AddressDetail,
                IsDefault = shouldBeDefault
            };

            _context.UserAddress.Add(address);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                address.UserAddressId,
                address.RecipientName,
                address.RecipientPhone,
                address.PostalCode,
                address.AddressDetail,
                address.IsDefault
            });
        }


        // PUT: api/UserAddress/me/5
        // 修改自己的某一筆地址
        // PUT: api/UserAddress/me/5
        // 修改自己的某一筆地址
        [HttpPut("me/{addressId}")]
        public async Task<IActionResult> UpdateMyAddress(
            int addressId,
            UserAddressDTO dto)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            // 找地址，而且必須是目前登入會員自己的
            var address = await _context.UserAddress
                .FirstOrDefaultAsync(a =>
                    a.UserAddressId == addressId &&
                    a.UserId == userId);

            if (address == null)
            {
                return NotFound("找不到收件地址");
            }

            // 記住修改前是不是預設地址
            var wasDefault = address.IsDefault == true;

            // ==================================
            // 處理預設地址
            // ==================================

            // 使用者要把這筆地址設為預設
            if (dto.IsDefault == true)
            {
                // 把其他地址全部取消預設
                var otherAddresses = await _context.UserAddress
                    .Where(a =>
                        a.UserId == userId &&
                        a.UserAddressId != addressId)
                    .ToListAsync();

                foreach (var item in otherAddresses)
                {
                    item.IsDefault = false;
                }

                address.IsDefault = true;
            }

            // 這筆原本就是預設地址
            // 不允許直接取消，避免沒有任何預設地址
            else if (wasDefault)
            {
                address.IsDefault = true;
            }

            // 原本不是預設，而且也沒有要求設成預設
            else
            {
                address.IsDefault = false;
            }


            // ==================================
            // 修改收件資料
            // ==================================

            address.RecipientName = dto.RecipientName;
            address.RecipientPhone = dto.RecipientPhone;
            address.PostalCode = dto.PostalCode;
            address.AddressDetail = dto.AddressDetail;

            await _context.SaveChangesAsync();

            return NoContent();
        }


        // PUT: api/UserAddress/me/5/default
        // 將指定地址設為預設地址
        [HttpPut("me/{addressId}/default")]
        public async Task<IActionResult> SetDefaultAddress(
            int addressId)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var address = await _context.UserAddress
                .FirstOrDefaultAsync(a =>
                    a.UserAddressId == addressId &&
                    a.UserId == userId);

            if (address == null)
            {
                return NotFound("找不到收件地址");
            }

            // 先取消目前所有預設
            var addresses = await _context.UserAddress
                .Where(a => a.UserId == userId)
                .ToListAsync();

            foreach (var item in addresses)
            {
                item.IsDefault =
                    item.UserAddressId == addressId;
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }


        // DELETE: api/UserAddress/me/5
        [HttpDelete("me/{addressId}")]
        public async Task<IActionResult> DeleteMyAddress(
            int addressId)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var address = await _context.UserAddress
                .FirstOrDefaultAsync(a =>
                    a.UserAddressId == addressId &&
                    a.UserId == userId);

            if (address == null)
            {
                return NotFound("找不到收件地址");
            }

            var wasDefault = address.IsDefault == true;

            _context.UserAddress.Remove(address);

            await _context.SaveChangesAsync();

            // 如果刪掉的是預設地址
            // 自動把剩下第一筆設為預設
            if (wasDefault)
            {
                var nextAddress = await _context.UserAddress
                    .Where(a => a.UserId == userId)
                    .OrderBy(a => a.UserAddressId)
                    .FirstOrDefaultAsync();

                if (nextAddress != null)
                {
                    nextAddress.IsDefault = true;

                    await _context.SaveChangesAsync();
                }
            }

            return NoContent();
        }
    }
}