namespace ECommerceAPI.Domain.Entities;

public partial class CustomerWallet
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public decimal AvailableBalance { get; set; }
    public string Currency { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual User Customer { get; set; } = null!;
    public virtual ICollection<CustomerWalletLedger> CustomerWalletLedgers { get; set; } = new List<CustomerWalletLedger>();
    public virtual ICollection<CustomerWithdrawalRequest> CustomerWithdrawalRequests { get; set; } = new List<CustomerWithdrawalRequest>();
}
