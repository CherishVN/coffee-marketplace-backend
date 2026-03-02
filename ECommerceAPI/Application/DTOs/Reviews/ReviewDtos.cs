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

