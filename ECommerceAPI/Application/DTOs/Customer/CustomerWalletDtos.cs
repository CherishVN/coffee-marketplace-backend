using FluentValidation;

namespace ECommerceAPI.Application.DTOs.Customer;

public class CustomerWalletDto
{
    public Guid Id { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal TotalRefunded { get; set; }
    public decimal TotalWithdrawn { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CustomerWalletLedgerItemDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CustomerWalletLedgerResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<CustomerWalletLedgerItemDto> Transactions { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class CreateCustomerWithdrawalDto
{
    public decimal Amount { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankAccountName { get; set; } = string.Empty;
}

public class CustomerWithdrawalRequestDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public decimal? AvailableBalance { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankAccountName { get; set; } = string.Empty;
    public short Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public string? AdminNote { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}

public class CustomerWithdrawalListResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<CustomerWithdrawalRequestDto> Requests { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class CustomerWithdrawalResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public CustomerWithdrawalRequestDto? Request { get; set; }
}

public class CreateCustomerWithdrawalDtoValidator : AbstractValidator<CreateCustomerWithdrawalDto>
{
    public CreateCustomerWithdrawalDtoValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền phải lớn hơn 0")
            .LessThanOrEqualTo(1_000_000_000).WithMessage("Số tiền không được vượt quá 1 tỷ");

        RuleFor(x => x.BankName)
            .NotEmpty().WithMessage("Tên ngân hàng không được để trống")
            .MaximumLength(100).WithMessage("Tên ngân hàng không được vượt quá 100 ký tự");

        RuleFor(x => x.BankAccountNumber)
            .NotEmpty().WithMessage("Số tài khoản không được để trống")
            .MaximumLength(50).WithMessage("Số tài khoản không hợp lệ");

        RuleFor(x => x.BankAccountName)
            .NotEmpty().WithMessage("Tên chủ tài khoản không được để trống")
            .MaximumLength(255).WithMessage("Tên chủ tài khoản không được vượt quá 255 ký tự");
    }
}
