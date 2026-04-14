using ECommerceAPI.Domain.Enums;
using FluentValidation;

namespace ECommerceAPI.Application.DTOs.Disputes;

public class CreateDisputeDto
{
    public Guid OrderId { get; set; }
    public short Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal RequestedAmount { get; set; }
    public List<string>? EvidenceUrls { get; set; }
}

public class UpdateEvidenceDto
{
    public List<string> EvidenceUrls { get; set; } = new();
    /// <summary>Phản hồi bổ sung bằng chữ khi admin yêu cầu thêm thông tin</summary>
    public string? CustomerNote { get; set; }
}

public class CustomerDisputeDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
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
    public bool CanUpdateEvidence { get; set; }
    public string? CustomerNote { get; set; }
}

public class CustomerDisputeListResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<CustomerDisputeDto> Disputes { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class CustomerDisputeResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public CustomerDisputeDto? Dispute { get; set; }
}

public class CreateDisputeDtoValidator : AbstractValidator<CreateDisputeDto>
{
    public CreateDisputeDtoValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty().WithMessage("OrderId không được để trống");

        RuleFor(x => x.Type)
            .InclusiveBetween((short)0, (short)6)
            .WithMessage("Loại khiếu nại không hợp lệ (0=Refund, 1=Return, 2=Damaged, 3=NotReceived, 4=WrongItem, 5=QualityIssue, 6=Other)");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề không được để trống")
            .MaximumLength(255).WithMessage("Tiêu đề không được vượt quá 255 ký tự");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do khiếu nại không được để trống")
            .MinimumLength(20).WithMessage("Lý do phải có ít nhất 20 ký tự")
            .MaximumLength(2000).WithMessage("Lý do không được vượt quá 2000 ký tự");

        RuleFor(x => x.RequestedAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền yêu cầu phải >= 0");

        RuleFor(x => x.EvidenceUrls)
            .Must(urls => urls == null || urls.Count <= 10)
            .WithMessage("Tối đa 10 file bằng chứng khi tạo khiếu nại");
    }
}

public class UpdateEvidenceDtoValidator : AbstractValidator<UpdateEvidenceDto>
{
    public UpdateEvidenceDtoValidator()
    {
        RuleFor(x => x.EvidenceUrls)
            .NotNull().WithMessage("Danh sách bằng chứng không được null")
            .Must(urls => urls.Count <= 10)
            .WithMessage("Tối đa 10 file bằng chứng");

        RuleFor(x => x.CustomerNote)
            .MaximumLength(2000).WithMessage("Phản hồi bổ sung không được vượt quá 2000 ký tự")
            .When(x => x.CustomerNote != null);
    }
}
