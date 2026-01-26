using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class SellerWalletLedger
{
    public Guid Id { get; set; }

    public Guid WalletId { get; set; }

    public string Type { get; set; } = null!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual SellerWallet Wallet { get; set; } = null!;
}
