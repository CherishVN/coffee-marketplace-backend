namespace ECommerceAPI.Application;

/// <summary>Giá trị ReferenceType trên seller_wallet_ledger để phân loại giao dịch.</summary>
public static class WalletLedgerReferenceTypes
{
    public const string OrderSettlement = "order_settlement";
    public const string OrderRefund = "order_refund";
    public const string Withdrawal = "withdrawal";
}
