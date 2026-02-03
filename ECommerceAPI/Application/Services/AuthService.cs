using System.Text;
using System.Text.Json;
using ECommerceAPI.Application.DTOs.Auth;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;

namespace ECommerceAPI.Application.Services;

public class AuthService : IAuthService
{
    private readonly IConfiguration _configuration;
    private readonly IJwtService _jwtService;
    private readonly IUserRepository _userRepository;

    public AuthService(
        IConfiguration configuration,
        IJwtService jwtService,
        IUserRepository userRepository)
    {
        _configuration = configuration;
        _jwtService = jwtService;
        _userRepository = userRepository;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginDto dto)
    {
        var supabaseUrl = _configuration["Supabase:Url"];
        var anonKey = _configuration["Supabase:AnonKey"];

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("apikey", anonKey);

        var requestBody = new { email = dto.Email, password = dto.Password };
        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{supabaseUrl}/auth/v1/token?grant_type=password", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new LoginResponseDto { Success = false, Message = "Email hoặc mật khẩu không đúng" };
        }

        var jsonDoc = JsonDocument.Parse(responseContent);
        var root = jsonDoc.RootElement;
        var userElement = root.GetProperty("user");

        var userId = Guid.Parse(userElement.GetProperty("id").GetString()!);
        var email = userElement.GetProperty("email").GetString();

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            user = await _userRepository.CreateAsync(new User
            {
                Id = userId,
                Role = "customer",
                Status = 1
            });
        }

        if (user.Status == 0)
        {
            return new LoginResponseDto
            {
                Success = false,
                Message = "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ admin."
            };
        }

        var token = _jwtService.GenerateToken(user, email);
        var expiresAt = _jwtService.GetTokenExpiration();

        return new LoginResponseDto
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
        };
    }

    public async Task<LoginResponseDto> RegisterAsync(RegisterDto dto)
    {
        var supabaseUrl = _configuration["Supabase:Url"];
        var anonKey = _configuration["Supabase:AnonKey"];

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("apikey", anonKey);

        var requestBody = new
        {
            email = dto.Email,
            password = dto.Password,
            data = new { full_name = dto.FullName }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync($"{supabaseUrl}/auth/v1/signup", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var errorDoc = JsonDocument.Parse(responseContent);
            var errorMsg = errorDoc.RootElement.TryGetProperty("msg", out var msg) ? msg.GetString() : "Đăng ký thất bại";
            return new LoginResponseDto { Success = false, Message = errorMsg };
        }

        var jsonDoc = JsonDocument.Parse(responseContent);
        var root = jsonDoc.RootElement;

        if (root.TryGetProperty("user", out var userElement) && userElement.ValueKind != JsonValueKind.Null)
        {
            var userId = Guid.Parse(userElement.GetProperty("id").GetString()!);
            var email = userElement.GetProperty("email").GetString();

            var existingUser = await _userRepository.GetByIdAsync(userId);
            var user = existingUser ?? await _userRepository.CreateAsync(new User
            {
                Id = userId,
                FullName = dto.FullName,
                Role = "customer",
                Status = 1
            });

            if (root.TryGetProperty("access_token", out _))
            {
                var token = _jwtService.GenerateToken(user, email);
                var expiresAt = _jwtService.GetTokenExpiration();

                return new LoginResponseDto
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
                };
            }

            return new LoginResponseDto
            {
                Success = true,
                Message = "Đăng ký thành công! Vui lòng kiểm tra email để xác nhận tài khoản."
            };
        }

        return new LoginResponseDto
        {
            Success = true,
            Message = "Đăng ký thành công! Vui lòng kiểm tra email để xác nhận tài khoản."
        };
    }
}
