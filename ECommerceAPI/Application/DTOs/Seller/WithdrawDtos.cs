using FluentValidation;

namespace ECommerceAPI.Application.DTOs.Seller;

// Withdrawal DTOs
public class CreateWithdrawalRequestDto
{
    public decimal Amount { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankAccountName { get; set; } = string.Empty;
}

public class WithdrawalRequestDto
{
    public Guid Id { get; set; }
    public Guid SellerId { get; set; }
    public decimal Amount { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankAccountName { get; set; } = string.Empty;
    public short Status { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? AdminNote { get; set; }
}

public class WalletDto
{
    public Guid Id { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal PendingBalance { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal TotalWithdrawn { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Validator
public class CreateWithdrawalRequestDtoValidator : AbstractValidator<CreateWithdrawalRequestDto>
{
    public CreateWithdrawalRequestDtoValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền phải lớn hơn 0")
            .LessThanOrEqualTo(1000000000).WithMessage("Số tiền không được vượt quá 1 tỷ");

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
