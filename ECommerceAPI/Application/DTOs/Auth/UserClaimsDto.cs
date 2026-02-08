namespace ECommerceAPI.Application.DTOs.Auth;

public class UserClaimsDto
{
    public Guid UserId { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
}
