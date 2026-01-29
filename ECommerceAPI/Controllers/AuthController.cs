using System.Text;
using System.Text.Json;
using ECommerceAPI.Application.DTOs.Auth;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IJwtService _jwtService;

    public AuthController(
        ApplicationDbContext context, 
        IConfiguration configuration,
        IJwtService jwtService)
    {
        _context = context;
        _configuration = configuration;
        _jwtService = jwtService;
    }

    /// <summary>
    /// Đăng nhập
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var supabaseUrl = _configuration["Supabase:Url"];
        var anonKey = _configuration["Supabase:AnonKey"];

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("apikey", anonKey);

        var requestBody = new
        {
            email = dto.Email,
            password = dto.Password
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json"
        );

        var response = await httpClient.PostAsync(
            $"{supabaseUrl}/auth/v1/token?grant_type=password",
            content
        );

        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return BadRequest(new LoginResponseDto
            {
                Success = false,
                Message = "Email hoặc mật khẩu không đúng"
            });
        }

        var jsonDoc = JsonDocument.Parse(responseContent);
        var root = jsonDoc.RootElement;
        var userElement = root.GetProperty("user");

        var userId = Guid.Parse(userElement.GetProperty("id").GetString()!);
        var email = userElement.GetProperty("email").GetString();

        // Tìm hoặc tạo user trong bảng public.users
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            user = new User
            {
                Id = userId,
                Role = "customer",
                Status = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        // Generate JWT token
        var token = _jwtService.GenerateToken(user, email);
        var expiresAt = _jwtService.GetTokenExpiration();

        return Ok(new LoginResponseDto
        {
            Success = true,
            Message = "Đăng nhập thành công",
            AccessToken = token,
            ExpiresAt = expiresAt,
            User = new UserProfileDto
            {
                Id = user.Id,
                Email = email,
                FullName = user.FullName,
                Phone = user.Phone,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            }
        });
    }

    /// <summary>
    /// Đăng ký tài khoản mới
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var supabaseUrl = _configuration["Supabase:Url"];
        var anonKey = _configuration["Supabase:AnonKey"];

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("apikey", anonKey);

        var requestBody = new
        {
            email = dto.Email,
            password = dto.Password,
            data = new
            {
                full_name = dto.FullName
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json"
        );

        var response = await httpClient.PostAsync(
            $"{supabaseUrl}/auth/v1/signup",
            content
        );

        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var errorDoc = JsonDocument.Parse(responseContent);
            var errorMsg = errorDoc.RootElement.TryGetProperty("msg", out var msg) 
                ? msg.GetString() 
                : "Đăng ký thất bại";
            
            return BadRequest(new LoginResponseDto
            {
                Success = false,
                Message = errorMsg
            });
        }

        var jsonDoc = JsonDocument.Parse(responseContent);
        var root = jsonDoc.RootElement;

        // Kiểm tra xem có user không
        if (root.TryGetProperty("user", out var userElement) && userElement.ValueKind != JsonValueKind.Null)
        {
            var userId = Guid.Parse(userElement.GetProperty("id").GetString()!);
            var email = userElement.GetProperty("email").GetString();

            // Tạo user trong bảng public.users
            var user = new User
            {
                Id = userId,
                FullName = dto.FullName,
                Role = "customer",
                Status = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Kiểm tra user đã tồn tại chưa
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (existingUser == null)
            {
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
            else
            {
                user = existingUser;
            }

            // Nếu email confirmation bị tắt, có thể login luôn
            if (root.TryGetProperty("access_token", out _))
            {
                var token = _jwtService.GenerateToken(user, email);
                var expiresAt = _jwtService.GetTokenExpiration();

                return Ok(new LoginResponseDto
                {
                    Success = true,
                    Message = "Đăng ký thành công",
                    AccessToken = token,
                    ExpiresAt = expiresAt,
                    User = new UserProfileDto
                    {
                        Id = user.Id,
                        Email = email,
                        FullName = user.FullName,
                        Role = user.Role,
                        CreatedAt = user.CreatedAt
                    }
                });
            }

            // Nếu cần xác nhận email
            return Ok(new LoginResponseDto
            {
                Success = true,
                Message = "Đăng ký thành công! Vui lòng kiểm tra email để xác nhận tài khoản."
            });
        }

        return Ok(new LoginResponseDto
        {
            Success = true,
            Message = "Đăng ký thành công! Vui lòng kiểm tra email để xác nhận tài khoản."
        });
    }
}

public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? FullName { get; set; }
}

public class LoginResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? AccessToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public UserProfileDto? User { get; set; }
}
