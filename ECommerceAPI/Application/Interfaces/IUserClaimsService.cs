using ECommerceAPI.Application.DTOs.Auth;

namespace ECommerceAPI.Application.Interfaces;

public interface IUserClaimsService
{
    Guid? GetUserId();
    string? GetEmail();
    string? GetFullName();
    UserClaimsDto? GetUserClaims();
}
