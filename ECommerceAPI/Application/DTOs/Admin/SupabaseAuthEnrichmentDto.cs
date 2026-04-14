namespace ECommerceAPI.Application.DTOs.Admin;

public class SupabaseAuthEnrichmentDto
{
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public SupabaseAuthInfoDto Details { get; set; } = new();
}
