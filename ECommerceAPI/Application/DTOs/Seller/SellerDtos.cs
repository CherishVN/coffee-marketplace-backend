using ECommerceAPI.Application.DTOs.Orders;
using ECommerceAPI.Domain.Enums;
using FluentValidation;

namespace ECommerceAPI.Application.DTOs.Seller;

// Shop Management DTOs
public class UpdateShopDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine { get; set; }
    public string? WardCode { get; set; }
    public int? DistrictId { get; set; }
    public int? ProvinceId { get; set; }
    public string? City { get; set; }
    public int? GhnShopId { get; set; }
}

// Product Management DTOs
public class CreateProductDto
{
    public long? CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public string Currency { get; set; } = "VND";
    public int Quantity { get; set; } = 0; // Dùng khi không có variants
    public List<ProductVariantDto>? Variants { get; set; }
    public List<string>? ImageUrls { get; set; }
    public List<long>? TagIds { get; set; }
    public List<Guid>? MaterialIds { get; set; }
}

public class UpdateProductDto
{
    public long? CategoryId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? BasePrice { get; set; }
    public short? Status { get; set; }
    public List<string>? ImageUrls { get; set; }
    public List<long>? TagIds { get; set; }
    public List<Guid>? MaterialIds { get; set; }
}

public class ProductVariantDto
{
    public string VariantName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public decimal? Price { get; set; }
    public int Quantity { get; set; }
    public string? Attributes { get; set; }
}

public class UpdateInventoryDto
{
    public Guid? VariantId { get; set; }
    public int Quantity { get; set; }
}

public class UpdateProductVariantDto
{
    public string? VariantName { get; set; }
    public string? Sku { get; set; }
    public decimal? Price { get; set; }
    public string? Attributes { get; set; }
    public bool? IsActive { get; set; }
}

// Order Management DTOs
public class SellerUpdateOrderStatusDto
{
    public short Status { get; set; }
    public string? Note { get; set; }
    public string? TrackingCode { get; set; }
}

// Response DTOs
public class ShopDto
{
    public Guid Id { get; set; }
    public string ShopCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine { get; set; }
    public string? WardCode { get; set; }
    public int? DistrictId { get; set; }
    public int? ProvinceId { get; set; }
    public string? City { get; set; }
    public int? GhnShopId { get; set; }
    public short Status { get; set; }
    public short VerificationStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProductDto
{
    public Guid Id { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public Guid ShopId { get; set; }
    public long? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public string Currency { get; set; } = string.Empty;
    public short Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ProductImageDto>? Images { get; set; }
    public List<ProductVariantDetailDto>? Variants { get; set; }
    public int? TotalStock { get; set; }
    public List<long>? TagIds { get; set; }
    public List<Guid>? MaterialIds { get; set; }
}

public class ProductImageDto
{
    public Guid Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public short DisplayOrder { get; set; }
}

public class ProductVariantDetailDto
{
    public Guid Id { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public decimal? Price { get; set; }
    public bool IsActive { get; set; }
    public int? Stock { get; set; }
    public string? Attributes { get; set; }
}

public class OrderDto
{
    public Guid Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerAvatarUrl { get; set; }
    public string? CustomerPhone { get; set; }
    public string? ShipPhone { get; set; }
    public decimal TotalAmount { get; set; }
    public short Status { get; set; }
    public string? CancelReason { get; set; }
    public string? ShippingAddress { get; set; }
    public decimal ShippingFee { get; set; }
    public string? ShippingProvider { get; set; }
    public string? ShippingServiceId { get; set; }
    public string? TrackingCode { get; set; }
    public DateTimeOffset? EstimatedDeliveryDate { get; set; }
    public DateTimeOffset? ActualDeliveryDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<OrderStatusHistoryItemDto> StatusHistory { get; set; } = new();
    public List<OrderStatusStepDto> StatusTimeline { get; set; } = new();
    public List<OrderItemDto>? Items { get; set; }

    public int? ShopGhnShopId { get; set; }
    public int? ShopFromDistrictId { get; set; }
    public string? ShopFromWardCode { get; set; }
    /// <summary>Thời điểm khách gửi yêu cầu hủy đang chờ shop duyệt. Null = không có yêu cầu.</summary>
    public DateTimeOffset? CancelRequestedAt { get; set; }
    /// <summary>Hạn shop phải phê duyệt / từ chối.</summary>
    public DateTimeOffset? CancelRequestDeadline { get; set; }

    /// <summary>Tiền hàng (subtotal) — cơ sở tính phí sàn. Chỉ gửi khi tải chi tiết.</summary>
    public decimal? Subtotal { get; set; }

    /// <summary>Tỷ lệ phí sàn đang áp dụng (0–100) tại thời điểm xem chi tiết.</summary>
    public decimal? PlatformFeePercent { get; set; }

    /// <summary>Ước tính tiền về seller sau phí sàn: Subtotal × (1 − PlatformFeePercent/100).</summary>
    public decimal? EstimatedNetAfterPlatformFee { get; set; }

    /// <summary>True khi đã có bản ghi phí sàn (chưa bị hoàn tác) cho đơn này.</summary>
    public bool? PlatformFeeSettled { get; set; }

    /// <summary>Số tiền phí sàn thực tế nếu đã quyết toán; null nếu chưa.</summary>
    public decimal? PlatformFeeAmount { get; set; }

    /// <summary>Số về seller sau khi trừ phí sàn nếu đã quyết toán; null nếu chưa.</summary>
    public decimal? NetToSellerAfterPlatformFee { get; set; }
}

public class RejectCancelRequestDto
{
    public string? Note { get; set; }
}

public class OrderItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductThumbnailUrl { get; set; }
    public string? VariantName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

// Validators
public class UpdateShopDtoValidator : AbstractValidator<UpdateShopDto>
{
    public UpdateShopDtoValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(255).WithMessage("Tên shop không được vượt quá 255 ký tự")
            .When(x => !string.IsNullOrEmpty(x.Name));

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Mô tả không được vượt quá 2000 ký tự")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Phone)
            .Matches(@"^(0|\+84)[0-9]{9,10}$").WithMessage("Số điện thoại không hợp lệ")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.AddressLine)
            .MaximumLength(500).WithMessage("Địa chỉ không được vượt quá 500 ký tự")
            .When(x => !string.IsNullOrEmpty(x.AddressLine));

        RuleFor(x => x.WardCode)
            .MaximumLength(50).WithMessage("Mã phường/xã không được vượt quá 50 ký tự")
            .When(x => !string.IsNullOrEmpty(x.WardCode));

        RuleFor(x => x.City)
            .MaximumLength(100).WithMessage("Tên thành phố không được vượt quá 100 ký tự")
            .When(x => !string.IsNullOrEmpty(x.City));

        RuleFor(x => x.DistrictId)
            .GreaterThan(0).WithMessage("district_id phải lớn hơn 0")
            .When(x => x.DistrictId.HasValue);

        RuleFor(x => x.ProvinceId)
            .GreaterThan(0).WithMessage("province_id phải lớn hơn 0")
            .When(x => x.ProvinceId.HasValue);

        RuleFor(x => x.GhnShopId)
            .GreaterThan(0).WithMessage("ghn_shop_id phải lớn hơn 0")
            .When(x => x.GhnShopId.HasValue);
    }
}

public class CreateProductDtoValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên sản phẩm không được để trống")
            .MaximumLength(500).WithMessage("Tên sản phẩm không được vượt quá 500 ký tự");

        RuleFor(x => x.Description)
            .MaximumLength(5000).WithMessage("Mô tả không được vượt quá 5000 ký tự")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.BasePrice)
            .GreaterThan(0).WithMessage("Giá phải lớn hơn 0");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Loại tiền tệ không được để trống")
            .MaximumLength(10).WithMessage("Loại tiền tệ không hợp lệ");
    }
}

public class UpdateProductDtoValidator : AbstractValidator<UpdateProductDto>
{
    public UpdateProductDtoValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(500).WithMessage("Tên sản phẩm không được vượt quá 500 ký tự")
            .When(x => !string.IsNullOrEmpty(x.Name));

        RuleFor(x => x.Description)
            .MaximumLength(5000).WithMessage("Mô tả không được vượt quá 5000 ký tự")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.BasePrice)
            .GreaterThan(0).WithMessage("Giá phải lớn hơn 0")
            .When(x => x.BasePrice.HasValue);

        RuleFor(x => x.Status)
            .InclusiveBetween((short)0, (short)5).WithMessage("Trạng thái không hợp lệ")
            .When(x => x.Status.HasValue);
    }
}

public class UpdateInventoryDtoValidator : AbstractValidator<UpdateInventoryDto>
{
    public UpdateInventoryDtoValidator()
    {
        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0).WithMessage("Số lượng phải >= 0");
    }
}

public class ProductVariantDtoValidator : AbstractValidator<ProductVariantDto>
{
    public ProductVariantDtoValidator()
    {
        RuleFor(x => x.VariantName)
            .NotEmpty().WithMessage("Tên biến thể không được để trống")
            .MaximumLength(255).WithMessage("Tên biến thể không quá 255 ký tự");

        RuleFor(x => x.Sku)
            .MaximumLength(100).WithMessage("SKU không quá 100 ký tự")
            .When(x => !string.IsNullOrEmpty(x.Sku));

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0).WithMessage("Số lượng tồn phải >= 0");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Giá biến thể phải lớn hơn 0")
            .When(x => x.Price.HasValue);

        RuleFor(x => x.Attributes)
            .MaximumLength(2000).WithMessage("Thuộc tính không quá 2000 ký tự")
            .When(x => !string.IsNullOrEmpty(x.Attributes));
    }
}

public class SellerUpdateOrderStatusDtoValidator : AbstractValidator<SellerUpdateOrderStatusDto>
{
    public SellerUpdateOrderStatusDtoValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => Enum.IsDefined(typeof(OrderStatus), s))
            .WithMessage("Trạng thái đơn hàng không hợp lệ");
    }
}
