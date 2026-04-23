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
    /// <summary>Tiền tạm giữ (đã thanh toán; rút được khi đơn hoàn thành sau cửa sổ khiếu nại và không khiếu nại mở).</summary>
    public decimal HeldBalance { get; set; }
    /// <summary>Tiền đang chờ duyệt rút (yêu cầu rút).</summary>
    public decimal PendingBalance { get; set; }
    /// <summary>Gross credits to wallet (sum of positive ledger), before refunds.</summary>
    public decimal TotalEarnings { get; set; }
    /// <summary>Net after order refunds: gross credits minus refunds (min 0).</summary>
    public decimal NetEarningsAfterRefunds { get; set; }
    /// <summary>Tổng đã rút (ledger withdrawal, sau khi admin duyệt).</summary>
    public decimal TotalWithdrawn { get; set; }
    /// <summary>Tổng đã hoàn tác do hủy/hoàn đơn (ledger order_refund).</summary>
    public decimal TotalRefunded { get; set; }
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
