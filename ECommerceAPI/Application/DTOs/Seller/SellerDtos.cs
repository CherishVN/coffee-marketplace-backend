using FluentValidation;

namespace ECommerceAPI.Application.DTOs.Seller;

// Shop Management DTOs
public class UpdateShopDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
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
    public List<long>? MaterialIds { get; set; }
}

public class UpdateProductDto
{
    public long? CategoryId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? BasePrice { get; set; }
    public short? Status { get; set; }
    public List<string>? ImageUrls { get; set; }
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

// Order Management DTOs
public class SellerUpdateOrderStatusDto
{
    public short Status { get; set; }
    public string? Note { get; set; }
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
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public decimal TotalAmount { get; set; }
    public short Status { get; set; }
    public string? ShippingAddress { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OrderItemDto>? Items { get; set; }
}

public class OrderItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
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
            .InclusiveBetween((short)0, (short)3).WithMessage("Trạng thái không hợp lệ")
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

public class SellerUpdateOrderStatusDtoValidator : AbstractValidator<SellerUpdateOrderStatusDto>
{
    public SellerUpdateOrderStatusDtoValidator()
    {
        RuleFor(x => x.Status)
            .InclusiveBetween((short)0, (short)5).WithMessage("Trạng thái đơn hàng không hợp lệ");
    }
}
