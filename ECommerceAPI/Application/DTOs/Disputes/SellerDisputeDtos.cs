using ECommerceAPI.Domain.Enums;
using FluentValidation;

namespace ECommerceAPI.Application.DTOs.Disputes;

public class SellerDisputeDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid ShopId { get; set; }
    public short Type { get; set; }
    public string TypeName => ((DisputeType)Type).ToString();
    public short Status { get; set; }
    public string StatusName => ((DisputeStatus)Status).ToString();
    public string Title { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal RequestedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string? Resolution { get; set; }
    public List<string> EvidenceUrls { get; set; } = new();
    public List<string> SellerEvidenceUrls { get; set; } = new();
    public string? SellerResponse { get; set; }
    public DateTime? SellerRespondedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool CanRespond { get; set; }
}

public class SellerDisputeListResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<SellerDisputeDto> Disputes { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class SellerDisputeResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public SellerDisputeDto? Dispute { get; set; }
}

public class SellerRespondDisputeDto
{
    public string Response { get; set; } = string.Empty;
    public List<string>? EvidenceUrls { get; set; }
}

public class SellerRespondDisputeDtoValidator : AbstractValidator<SellerRespondDisputeDto>
{
    public SellerRespondDisputeDtoValidator()
    {
        RuleFor(x => x.Response)
            .NotEmpty().WithMessage("Nội dung phản hồi không được để trống")
            .MinimumLength(10).WithMessage("Phản hồi phải có ít nhất 10 ký tự")
            .MaximumLength(2000).WithMessage("Phản hồi không được vượt quá 2000 ký tự");

        RuleFor(x => x.EvidenceUrls)
            .Must(urls => urls == null || urls.Count <= 10)
            .WithMessage("Tối đa 10 file bằng chứng");
    }
}
