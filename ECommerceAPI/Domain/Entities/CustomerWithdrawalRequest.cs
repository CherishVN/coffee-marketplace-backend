namespace ECommerceAPI.Domain.Entities;

public partial class CustomerWithdrawalRequest
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid WalletId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
    public string BankName { get; set; } = null!;
    public string BankAccountNumber { get; set; } = null!;
    public string BankAccountName { get; set; } = null!;

    /// <summary>0 = Pending, 1 = Approved, 2 = Rejected, 3 = Paid</summary>
    public short Status { get; set; }

    public decimal? WalletBalanceAtRequest { get; set; }

    public string? RejectionReason { get; set; }
    public string? AdminNote { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? PaidAt { get; set; }

    public virtual User Customer { get; set; } = null!;
    public virtual CustomerWallet Wallet { get; set; } = null!;
    public virtual User? ReviewedByNavigation { get; set; }
}
