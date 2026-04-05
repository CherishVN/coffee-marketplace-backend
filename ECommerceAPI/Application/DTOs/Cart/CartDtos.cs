namespace ECommerceAPI.Application.DTOs.Cart;

// ── Request DTOs ──────────────────────────────────────────────────────────────

public class AddCartItemDto
{
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class UpdateCartItemDto
{
    public int Quantity { get; set; }
}

public class ShopShippingOptionDto
{
    public Guid ShopId { get; set; }
    public string? ShippingProvider { get; set; }
    public string? ShippingServiceId { get; set; }
    public decimal ProviderShippingFee { get; set; }
    public decimal ShippingFee { get; set; }
    public DateTimeOffset? EstimatedDeliveryDate { get; set; }
}

public class CheckoutDto
{
    public Guid CartId { get; set; }
    public Guid ShippingAddressId { get; set; }
    public List<ShopShippingOptionDto>? ShippingOptions { get; set; }
}

// ── Response DTOs ─────────────────────────────────────────────────────────────

public class CartDto
{
    public Guid Id { get; set; }
    public short Status { get; set; }
    public List<CartItemDto> Items { get; set; } = new();
    public decimal Subtotal => Items.Sum(i => i.LineTotal);
    public int TotalItems => Items.Sum(i => i.Quantity);
    public DateTime UpdatedAt { get; set; }
}

public class CartItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string? ProductImage { get; set; }
    public Guid? VariantId { get; set; }
    public string? VariantName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
    public int StockAvailable { get; set; }
}

public class CheckoutResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<Guid> OrderIds { get; set; } = new();
    public decimal TotalAmount { get; set; }
}
