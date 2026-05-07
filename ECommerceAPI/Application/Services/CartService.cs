using ECommerceAPI.Application.DTOs.Cart;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ECommerceAPI.Application.Services;

public class CartService : ICartService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IOrderStatusHistoryService _orderStatusHistory;
    private readonly INotificationService _notifications;
    private readonly ILogger<CartService> _logger;

    public CartService(
        ApplicationDbContext context,
        IConfiguration configuration,
        IOrderStatusHistoryService orderStatusHistory,
        INotificationService notifications,
        ILogger<CartService> logger)
    {
        _context = context;
        _configuration = configuration;
        _orderStatusHistory = orderStatusHistory;
        _notifications = notifications;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> RemoveAllCartItemsForProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var rows = await _context.CartItems
            .AsNoTracking()
            .Where(ci => ci.ProductId == productId)
            .Select(ci => new { ci.Cart.CustomerId, ProductName = ci.Product.Name })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return 0;

        var productName = string.IsNullOrWhiteSpace(rows[0].ProductName)
            ? "Sản phẩm"
            : rows[0].ProductName.Trim();
        var customerIds = rows.Select(r => r.CustomerId).Distinct().ToList();

        var deleted = await _context.CartItems
            .Where(ci => ci.ProductId == productId)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var customerId in customerIds)
        {
            try
            {
                await _notifications.PublishAsync(
                    customerId,
                    nameof(NotificationType.System),
                    "Sản phẩm đã được gỡ khỏi giỏ hàng",
                    $"Sản phẩm \"{productName}\" không còn hiển thị trên cửa hàng và đã được tự động gỡ khỏi giỏ hàng của bạn.",
                    "Product",
                    productId,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Không gửi được thông báo gỡ giỏ cho khách {CustomerId}, sản phẩm {ProductId}",
                    customerId,
                    productId);
            }
        }

        return deleted;
    }

    // ── Xem giỏ hàng ────────────────────────────────────────────────────────
    public async Task<CartDto?> GetMyCartAsync(Guid customerId)
    {
        var cart = await _context.Carts
            .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                    .ThenInclude(p => p.ProductImages)
            .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                    .ThenInclude(p => p.Shop)
            .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Variant)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Status == 0);

        if (cart == null) return null;

        var result = MapToCartDto(cart);

        var inventories = await _context.Inventories
            .Where(i => cart.CartItems.Select(ci => ci.ProductId).Contains(i.ProductId))
            .Select(i => new
            {
                i.ProductId,
                i.VariantId,
                Available = Math.Max(0, i.Quantity - i.ReservedQuantity)
            })
            .ToListAsync();

        var inventoryMap = inventories.ToDictionary(
            x => $"{x.ProductId}:{x.VariantId}",
            x => x.Available
        );

        foreach (var item in result.Items)
        {
            var key = $"{item.ProductId}:{item.VariantId}";
            item.StockAvailable = inventoryMap.TryGetValue(key, out var available)
                ? available
                : 0;
        }

        return result;
    }

    // ── Thêm vào giỏ ─────────────────────────────────────────────────────────
    public async Task<(bool Success, string? Error, CartItemDto? Item)> AddItemAsync(Guid customerId, AddCartItemDto dto)
    {
        if (dto.Quantity <= 0)
            return (false, "Số lượng phải lớn hơn 0", null);

        // Kiểm tra product tồn tại và đang active
        var product = await _context.Products
            .Include(p => p.ProductImages)
            .FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.Status == 1);

        if (product == null)
            return (false, "Sản phẩm không tồn tại hoặc đã ngừng bán", null);

        // Kiểm tra variant (nếu có)
        ProductVariant? variant = null;
        if (dto.VariantId.HasValue)
        {
            variant = await _context.ProductVariants
                .FirstOrDefaultAsync(v => v.Id == dto.VariantId && v.ProductId == dto.ProductId && v.IsActive);
            if (variant == null)
                return (false, "Biến thể sản phẩm không tồn tại", null);
        }

        // Kiểm tra tồn kho
        var inventory = await _context.Inventories
            .FirstOrDefaultAsync(i => i.ProductId == dto.ProductId && i.VariantId == dto.VariantId);

        var available = inventory != null ? inventory.Quantity - inventory.ReservedQuantity : 0;
        if (available < dto.Quantity)
            return (false, $"Số lượng tồn kho không đủ (còn {available})", null);

        // Lấy hoặc tạo giỏ hàng active
        var cart = await _context.Carts
            .Include(c => c.CartItems)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Status == 0);

        if (cart == null)
        {
            cart = new Cart
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                Status = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Carts.Add(cart);
        }

        // Tính giá: variant.Price ưu tiên, fallback về product.BasePrice
        var unitPrice = variant?.Price ?? product.BasePrice;

        // Nếu sản phẩm đã có trong giỏ → tăng số lượng
        var existingItem = cart.CartItems
            .FirstOrDefault(ci => ci.ProductId == dto.ProductId && ci.VariantId == dto.VariantId);

        if (existingItem != null)
        {
            var newQty = existingItem.Quantity + dto.Quantity;
            if (available < newQty)
                return (false, $"Số lượng tồn kho không đủ", null);

            existingItem.Quantity = newQty;
            existingItem.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existingItem = new CartItem
            {
                Id = Guid.NewGuid(),
                CartId = cart.Id,
                ProductId = dto.ProductId,
                VariantId = dto.VariantId,
                ProductName = product.Name,
                UnitPrice = unitPrice,
                Quantity = dto.Quantity,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.CartItems.Add(existingItem);
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (true, null, new CartItemDto
        {
            Id = existingItem.Id,
            ProductId = dto.ProductId,
            ProductName = product.Name,
            ProductSlug = product.Slug,
            ProductImage = product.ProductImages.OrderBy(i => i.SortOrder).FirstOrDefault()?.ImageUrl,
            VariantId = dto.VariantId,
            VariantName = variant?.VariantName,
            UnitPrice = unitPrice,
            Quantity = existingItem.Quantity,
            StockAvailable = available
        });
    }

    // ── Cập nhật số lượng ────────────────────────────────────────────────────
    public async Task<(bool Success, string? Error)> UpdateItemAsync(Guid customerId, Guid itemId, UpdateCartItemDto dto)
    {
        if (dto.Quantity <= 0)
            return (false, "Số lượng phải lớn hơn 0");

        var item = await _context.CartItems
            .Include(ci => ci.Cart)
            .FirstOrDefaultAsync(ci => ci.Id == itemId && ci.Cart.CustomerId == customerId && ci.Cart.Status == 0);

        if (item == null)
            return (false, "Không tìm thấy sản phẩm trong giỏ hàng");

        // Kiểm tra tồn kho
        var inventory = await _context.Inventories
            .FirstOrDefaultAsync(i => i.ProductId == item.ProductId && i.VariantId == item.VariantId);

        var available = inventory != null ? inventory.Quantity - inventory.ReservedQuantity : 0;
        if (available < dto.Quantity)
            return (false, $"Số lượng tồn kho không đủ (còn {available})");

        item.Quantity = dto.Quantity;
        item.UpdatedAt = DateTime.UtcNow;
        item.Cart.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return (true, null);
    }

    // ── Xóa khỏi giỏ ────────────────────────────────────────────────────────
    public async Task<(bool Success, string? Error)> RemoveItemAsync(Guid customerId, Guid itemId)
    {
        var item = await _context.CartItems
            .Include(ci => ci.Cart)
            .FirstOrDefaultAsync(ci => ci.Id == itemId && ci.Cart.CustomerId == customerId && ci.Cart.Status == 0);

        if (item == null)
            return (false, "Không tìm thấy sản phẩm trong giỏ hàng");

        item.Cart.UpdatedAt = DateTime.UtcNow;
        _context.CartItems.Remove(item);

        await _context.SaveChangesAsync();
        return (true, null);
    }

    // ── Checkout (tạo đơn hàng từ giỏ) ──────────────────────────────────────
    public async Task<CheckoutResponseDto> CheckoutAsync(Guid customerId, CheckoutDto dto)
    {
        // 1. Lấy cart với đầy đủ thông tin
        var cart = await _context.Carts
            .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
            .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Variant)
            .FirstOrDefaultAsync(c => c.Id == dto.CartId && c.CustomerId == customerId && c.Status == 0);

        if (cart == null)
            return new CheckoutResponseDto { Success = false, Message = "Không tìm thấy giỏ hàng" };

        if (!cart.CartItems.Any())
            return new CheckoutResponseDto { Success = false, Message = "Giỏ hàng đang trống" };

        // 2. Lấy địa chỉ giao hàng
        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == dto.ShippingAddressId && a.UserId == customerId);

        if (address == null)
            return new CheckoutResponseDto { Success = false, Message = "Địa chỉ giao hàng không hợp lệ" };

        // 3. Kiểm tra tính hợp lệ của sản phẩm, giá cả và tồn kho
        bool priceChanged = false;

        foreach (var item in cart.CartItems)
        {
            // Kiểm tra trạng thái sản phẩm
            if (item.Product == null || item.Product.Status != 1)
            {
                return new CheckoutResponseDto
                {
                    Success = false,
                    Message = $"Sản phẩm '{item.ProductName}' đã ngừng bán hoặc không tồn tại."
                };
            }

            // Kiểm tra trạng thái phân loại (Variant)
            if (item.VariantId.HasValue && (item.Variant == null || !item.Variant.IsActive))
            {
                return new CheckoutResponseDto
                {
                    Success = false,
                    Message = $"Phân loại của sản phẩm '{item.ProductName}' đã ngừng bán."
                };
            }

            // Đồng bộ giá tiền hiện tại (Real-time Price)
            var currentPrice = item.VariantId.HasValue && item.Variant != null 
                ? item.Variant.Price ?? item.Product.BasePrice 
                : item.Product.BasePrice;

            if (item.UnitPrice != currentPrice)
            {
                item.UnitPrice = currentPrice;
                item.UpdatedAt = DateTime.UtcNow;
                priceChanged = true;
            }

            // Kiểm tra tồn kho
            var inv = await _context.Inventories
                .FirstOrDefaultAsync(i => i.ProductId == item.ProductId && i.VariantId == item.VariantId);

            var available = inv != null ? inv.Quantity - inv.ReservedQuantity : 0;
            if (available < item.Quantity)
                return new CheckoutResponseDto
                {
                    Success = false,
                    Message = $"Sản phẩm '{item.ProductName}' không đủ hàng (còn {available})."
                };
        }

        // Chặn thanh toán nếu giá đã thay đổi, yêu cầu người dùng kiểm tra lại giỏ hàng
        if (priceChanged)
        {
            await _context.SaveChangesAsync();
            return new CheckoutResponseDto
            {
                Success = false,
                Message = "Giá của một số sản phẩm đã thay đổi kể từ lúc bạn thêm vào giỏ. Hệ thống đã cập nhật lại giá mới, vui lòng kiểm tra lại giỏ hàng trước khi thanh toán."
            };
        }

        var itemsByShop = cart.CartItems
            .GroupBy(ci => ci.Product.ShopId)
            .ToList();

        var orderIds = new List<Guid>();
        var totalAmount = 0m;
        var shipAddress = $"{address.AddressLine1}, {address.Ward}, {address.District}, {address.City}";

        if (dto.ShippingOptions == null || dto.ShippingOptions.Count == 0)
        {
            return new CheckoutResponseDto
            {
                Success = false,
                Message =
                    "Thiếu phí vận chuyển theo từng shop. Vui lòng chọn địa chỉ hợp lệ và đợi hệ thống tính phí GHN (tỉnh/quận/xã phải khớp dữ liệu GHN)."
            };
        }

        foreach (var shopGroup in itemsByShop)
        {
            var shopId = shopGroup.Key;
            var shippingOption = dto.ShippingOptions.FirstOrDefault(x => x.ShopId == shopId);
            if (shippingOption == null)
            {
                return new CheckoutResponseDto
                {
                    Success = false,
                    Message =
                        "Thiếu phí vận chuyển cho một hoặc nhiều shop. Vui lòng tải lại trang checkout và tính lại phí GHN."
                };
            }

            var shippingFee = shippingOption.ShippingFee;
            var subtotal = shopGroup.Sum(ci => ci.UnitPrice * ci.Quantity);
            var total = subtotal + shippingFee;

            var estimatedDelivery = shippingOption?.EstimatedDeliveryDate;
            if (estimatedDelivery == null)
            {
                var fallbackDays = _configuration.GetValue("Orders:DefaultEstimatedDeliveryDays", 4);
                if (fallbackDays < 1) fallbackDays = 4;
                estimatedDelivery = DateTimeOffset.UtcNow.AddDays(fallbackDays);
            }

            var order = new Order
            {
                Id = Guid.NewGuid(),
                OrderCode = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper() + DateTime.Now.ToString("yyMMdd"),
                CustomerId = customerId,
                ShopId = shopId,
                Status = 0,
                Subtotal = subtotal,
                ShippingFee = shippingFee,
                Total = total,
                ShipFullName = address.FullName,
                ShipPhone = address.Phone,
                ShipAddress = shipAddress,
                ShippingAddressId = address.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Orders.Add(order);
            _orderStatusHistory.AddEntry(
                order.Id,
                null,
                0,
                customerId,
                "Tạo đơn từ giỏ hàng");

            foreach (var ci in shopGroup)
            {
                _context.OrderItems.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    ProductId = ci.ProductId,
                    VariantId = ci.VariantId,
                    ProductName = ci.ProductName,
                    UnitPrice = ci.UnitPrice,
                    Quantity = ci.Quantity,
                    LineTotal = ci.UnitPrice * ci.Quantity,
                    CreatedAt = DateTime.UtcNow
                });

                // Cộng reserved_quantity để giữ hàng
                var inv = await _context.Inventories
                    .FirstOrDefaultAsync(i => i.ProductId == ci.ProductId && i.VariantId == ci.VariantId);
                if (inv != null)
                {
                    inv.ReservedQuantity += ci.Quantity;
                    inv.UpdatedAt = DateTime.UtcNow;
                }
            }

            _context.Shipments.Add(new Shipment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ShopId = shopId,
                ShippingProvider = "GHN",
                ShippingServiceId = shippingOption?.ShippingServiceId,
                TrackingCode = $"PEND-{order.OrderCode}",
                Status = "pending_label",
                ProviderShippingFee = shippingFee,
                CodAmount = 0m,
                EstimatedDeliveryDate = estimatedDelivery,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            orderIds.Add(order.Id);
            totalAmount += total;
        }

        _context.CartItems.RemoveRange(cart.CartItems);
        cart.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new CheckoutResponseDto
        {
            Success = true,
            Message = $"Đặt hàng thành công! Tạo {orderIds.Count} đơn hàng.",
            OrderIds = orderIds,
            TotalAmount = totalAmount
        };
    }

    // ── Private helper ───────────────────────────────────────────────────────
    private static CartDto MapToCartDto(Cart cart)
    {
        return new CartDto
        {
            Id = cart.Id,
            Status = cart.Status,
            UpdatedAt = cart.UpdatedAt,
            Items = cart.CartItems.Select(ci => new CartItemDto
            {
                Id = ci.Id,
                ProductId = ci.ProductId,
                ProductName = ci.ProductName,
                ProductSlug = ci.Product.Slug,
                ProductImage = ci.Product.ProductImages.OrderBy(i => i.SortOrder).FirstOrDefault()?.ImageUrl,
                VariantId = ci.VariantId,
                VariantName = ci.Variant?.VariantName,
                UnitPrice = ci.UnitPrice,
                Quantity = ci.Quantity,
                StockAvailable = 0,
                ShopId = ci.Product.ShopId,
                ShopName = ci.Product.Shop?.Name,
                ShopSlug = ci.Product.Shop?.Slug,
                GhnShopId = ci.Product.Shop?.GhnShopId,
                FromDistrictId = ci.Product.Shop?.DistrictId,
                FromWardCode = ci.Product.Shop?.WardCode,
            }).ToList()
        };
    }
}
