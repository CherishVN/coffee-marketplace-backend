using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class SellerWallet
{
    public Guid Id { get; set; }

    public Guid SellerId { get; set; }

    public decimal AvailableBalance { get; set; }

    /// <summary>Tiền tạm giữ sau thanh toán; chuyển sang Available khi đơn Completed.</summary>
    public decimal HeldBalance { get; set; }

    public decimal PendingBalance { get; set; }

    public string Currency { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User Seller { get; set; } = null!;

    public virtual ICollection<SellerWalletLedger> SellerWalletLedgers { get; set; } = new List<SellerWalletLedger>();

    public virtual ICollection<SellerWithdrawalRequest> SellerWithdrawalRequests { get; set; } = new List<SellerWithdrawalRequest>();
}
