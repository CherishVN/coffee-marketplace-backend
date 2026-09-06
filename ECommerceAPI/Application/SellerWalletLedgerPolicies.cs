namespace ECommerceAPI.Application;

/// <summary>Quy tắc nghiệp vụ ghi trên ledger ví seller (không đọc appsettings).</summary>
public static class SellerWalletLedgerPolicies
{
    /// <summary>
    /// Số ngày sau lần đầu Đã giao để hết cửa sổ khiếu nại; sau đó đơn có thể Hoàn thành và ví được giải ngân (nếu không khiếu nại mở).
    /// </summary>
    public const int ReleaseDaysAfterOrderDelivered = 7;
}
