using CLOthings.Enums;
using CLOthings_API.DTO.User;
using CLOthings_API.DTOs;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CLOthings_API.Controllers
{
    [Route("api/User")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly CLOthingsContext _context;
        private readonly IConfiguration _configuration;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AuthController(
            CLOthingsContext context,
            IConfiguration configuration,
            IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _configuration = configuration;
            _passwordHasher = passwordHasher;
        }

        // POST: api/User/login
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult> Login(LoginDTO dto)
        {
            // 1. 根據帳號找使用者
            var user = await _context.User
                .FirstOrDefaultAsync(u => u.Account == dto.Account);

            if (user == null)
            {
                return Unauthorized("帳號或密碼錯誤");
            }

            // 2. 驗證密碼
            var passwordResult =
                _passwordHasher.VerifyHashedPassword(
                    user,
                    user.Password,
                    dto.Password
                );

            if (passwordResult == PasswordVerificationResult.Failed)
            {
                return Unauthorized("帳號或密碼錯誤");
            }

            // 3. 產生 Access Token
            var tokenString = GenerateAccessToken(user);

            // 4. 回傳登入資料
            return Ok(new
            {
                token = tokenString,
                name = user.Username,
                account = user.Account,
                role = ((UserTypeEnum)user.UserType).ToString()
            });
        }

        // 產生 JWT Access Token
        private string GenerateAccessToken(User user)
        {
            var role = ((UserTypeEnum)user.UserType).ToString();

            var claims = new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.UserId.ToString()
                ),

                new Claim(
                    ClaimTypes.Name,
                    user.Username
                ),

                new Claim(
                    "account",
                    user.Account
                ),

                new Claim(
                    ClaimTypes.Role,
                    role
                )
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _configuration["Jwt:Key"]!
                )
            );

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }
    }
}