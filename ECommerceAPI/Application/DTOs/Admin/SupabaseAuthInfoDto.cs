namespace ECommerceAPI.Application.DTOs.Admin;

/// <summary>Dữ liệu từ Supabase Auth (GoTrue) — admin API.</summary>
public class SupabaseAuthInfoDto
{
    /// <summary>Lần đăng nhập gần nhất (UTC).</summary>
    public DateTime? LastSignInAt { get; set; }

    /// <summary>Tạo tài khoản trên Supabase Auth (UTC).</summary>
    public DateTime? AuthUserCreatedAt { get; set; }

    /// <summary>Email đã xác nhận (UTC).</summary>
    public DateTime? EmailConfirmedAt { get; set; }

    /// <summary>Số điện thoại trên Auth (nếu có).</summary>
    public string? AuthPhone { get; set; }

    /// <summary>Display name từ user_metadata (nếu có).</summary>
    public string? AuthDisplayName { get; set; }

    /// <summary>Nhà cung cấp: email, google, ...</summary>
    public List<string> Providers { get; set; } = new();
}
