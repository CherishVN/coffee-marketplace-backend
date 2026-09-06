using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class SellerWithdrawalRequest
{
    public Guid Id { get; set; }

    public Guid SellerId { get; set; }

    public Guid WalletId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public string BankName { get; set; } = null!;

    public string BankAccountNumber { get; set; } = null!;

    public string BankAccountName { get; set; } = null!;

    public short Status { get; set; }

    public decimal? WalletBalanceAtRequest { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTime? PaidAt { get; set; }

    public string? AdminNote { get; set; }

    public virtual User? ReviewedByNavigation { get; set; }

    public virtual User Seller { get; set; } = null!;

    public virtual SellerWallet Wallet { get; set; } = null!;
}
