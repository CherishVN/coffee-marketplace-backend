using ECommerceAPI.Domain.Entities;

namespace ECommerceAPI.Application.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user, string? email = null);
    DateTime GetTokenExpiration();
}
