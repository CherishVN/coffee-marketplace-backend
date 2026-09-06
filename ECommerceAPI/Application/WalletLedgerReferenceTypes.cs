namespace ECommerceAPI.Application;

/// <summary>Giá trị ReferenceType trên seller_wallet_ledger để phân loại giao dịch.</summary>
public static class WalletLedgerReferenceTypes
{
    public const string OrderSettlement = "order_settlement";
    /// <summary>Đánh dấu đã giải ngân held → available (Amount thường = 0, không cộng trùng totalEarnings).</summary>
    public const string OrderRelease = "order_release";
    public const string OrderRefund = "order_refund";
    public const string Withdrawal = "withdrawal";
}
