namespace ECommerceAPI.Application.DTOs.Auth;

public class LoginResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? AccessToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public UserProfileDto? User { get; set; }
}
