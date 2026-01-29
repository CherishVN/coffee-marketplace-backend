using ECommerceAPI.Application.DTOs.Auth;

namespace ECommerceAPI.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginDto dto);
    Task<LoginResponseDto> RegisterAsync(RegisterDto dto);
}
