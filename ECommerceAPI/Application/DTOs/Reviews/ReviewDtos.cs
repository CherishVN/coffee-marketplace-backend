using FluentValidation;

namespace ECommerceAPI.Application.DTOs.Reviews;

public class CreateProductReviewDto
{
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public short Rating { get; set; }
    public string? Comment { get; set; }
    public List<string>? ImageUrls { get; set; }
}

public class ProductReviewDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public short Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public string? SellerReply { get; set; }
    /// <summary>Tạm thời 0 — có thể bổ sung vote hữu ích sau.</summary>
    public int HelpfulCount { get; set; }
}

public class ProductReviewStatsDto
{
    public int Total { get; set; }
    public int Count5 { get; set; }
    public int Count4 { get; set; }
    public int Count3 { get; set; }
    public int Count2 { get; set; }
    public int Count1 { get; set; }
    public int WithComment { get; set; }
    public int WithImage { get; set; }
}

public class ProductReviewStatsResponseDto
{
    public bool Success { get; set; }
    public ProductReviewStatsDto Data { get; set; } = new();
}

public class ProductReviewListResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<ProductReviewDto> Reviews { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class CreateShopReviewDto
{
    public Guid ShopId { get; set; }
    public Guid OrderId { get; set; }
    public short Rating { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
}

public class ShopReviewDto
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public short Rating { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ShopReviewListResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<ShopReviewDto> Reviews { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public double AverageRating { get; set; }
}

public class CreateShopReviewDtoValidator : AbstractValidator<CreateShopReviewDto>
{
    public CreateShopReviewDtoValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty().WithMessage("ShopId không được để trống");

        RuleFor(x => x.OrderId)
            .NotEmpty().WithMessage("OrderId không được để trống");

        RuleFor(x => x.Rating)
            .InclusiveBetween((short)1, (short)5)
            .WithMessage("Rating phải từ 1 đến 5 sao");

        RuleFor(x => x.Title)
            .MaximumLength(100)
            .WithMessage("Tiêu đề không được vượt quá 100 ký tự")
            .When(x => !string.IsNullOrWhiteSpace(x.Title));

        RuleFor(x => x.Content)
            .MaximumLength(500)
            .WithMessage("Nội dung không được vượt quá 500 ký tự")
            .When(x => !string.IsNullOrWhiteSpace(x.Content));
    }
}

public class CreateProductReviewDtoValidator : AbstractValidator<CreateProductReviewDto>
{
    public CreateProductReviewDtoValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty().WithMessage("OrderId không được để trống");

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("ProductId không được để trống");

        RuleFor(x => x.Rating)
            .InclusiveBetween((short)1, (short)5)
            .WithMessage("Rating phải từ 1 đến 5 sao");

        RuleFor(x => x.Comment)
            .MaximumLength(500)
            .WithMessage("Comment không được vượt quá 500 ký tự")
            .When(x => !string.IsNullOrWhiteSpace(x.Comment));

        RuleFor(x => x.ImageUrls)
            .Must(urls => urls == null || urls.Count <= 5)
            .WithMessage("Tối đa 5 ảnh cho mỗi đánh giá");
    }
}

