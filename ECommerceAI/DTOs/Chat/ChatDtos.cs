namespace ECommerceAI.DTOs.Chat;

// ── Request DTOs ──────────────────────────────────────────────────────────────

public class CreateSessionRequestDto
{
    public Guid UserId { get; set; }
}

public class SendMessageRequestDto
{
    public Guid SessionId { get; set; }
    public string Message { get; set; } = null!;
}

public class ConfirmOrderRequestDto
{
    public Guid SessionId { get; set; }
    public Guid CartId { get; set; }
    public Guid ShippingAddressId { get; set; }
}

// ── Response DTOs ─────────────────────────────────────────────────────────────

public class SessionResponseDto
{
    public Guid SessionId { get; set; }
    public string Status { get; set; } = null!;
    public List<ChatMessageDto> History { get; set; } = new();
}

public class ChatMessageDto
{
    public long Id { get; set; }
    public string Role { get; set; } = null!;  // "user" | "assistant"
    public string Content { get; set; } = null!;
    public DateTime? CreatedAt { get; set; }
}

public class SendMessageResponseDto
{
    public string Reply { get; set; } = null!;
    public string Intent { get; set; } = "general";    // product_search | add_to_cart | checkout | general
    public List<ProductSuggestionDto> Products { get; set; } = new();
    public bool NeedsConfirmation { get; set; }         // true → frontend hiển thị nút Có/Không
    public bool CartUpdated { get; set; }
    public Guid? CartId { get; set; }
    public Guid SessionId { get; set; }

    /// <summary>
    /// Thông tin sản phẩm AI muốn thêm vào giỏ hàng (khi intent = "add_to_cart").
    /// Frontend dùng thông tin này để gọi Main API: POST /api/cart/items
    /// sau đó lưu cartId và truyền vào confirm-order.
    /// </summary>
    public ProductToAddDto? ProductToAdd { get; set; }
}

public class ProductToAddDto
{
    public Guid? ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class ProductSuggestionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal BasePrice { get; set; }
    public string? ImageUrl { get; set; }
    public string? CategoryName { get; set; }
    public List<VariantSuggestionDto> Variants { get; set; } = new();
    public decimal? MatchScore { get; set; }
    public string? MatchReason { get; set; }
}

public class VariantSuggestionDto
{
    public Guid Id { get; set; }
    public string VariantName { get; set; } = null!;
    public decimal? Price { get; set; }
}

public class ConfirmOrderResponseDto
{
    public bool Success { get; set; }
    public Guid? OrderId { get; set; }
    public string Message { get; set; } = null!;
}
