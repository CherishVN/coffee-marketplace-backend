namespace ECommerceAPI.Domain.Entities;

public partial class CustomerWalletLedger
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }

    /// <summary>'refund' or 'withdrawal'</summary>
    public string Type { get; set; } = null!;

    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;

    /// <summary>'Order' or 'WithdrawalRequest'</summary>
    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual CustomerWallet Wallet { get; set; } = null!;
}
