namespace ECommerceAPI.Application.DTOs.Payments;

public class CreatePaymentDto
{
    public Guid OrderId { get; set; }

    /// <summary>
    /// Deep link (vd. ecommerce:// hoặc exp://) — BE redirect sau khi xử lý VNPay return, dùng với in-app browser.
    /// </summary>
    public string? ClientReturnSuccessUrl { get; set; }

    public string? ClientReturnFailureUrl { get; set; }

    /// <summary>
    /// (Tùy chọn) URL VNPay redirect về sau thanh toán — phải trỏ tới endpoint API /api/payments/vnpay/return.
    /// Mobile/emulator: gửi host khớp API (vd. http://10.0.2.2:5153/...) vì localhost trong WebView là emulator.
    /// Web: để trống để dùng cấu hình VNPay:ReturnUrl.
    /// </summary>
    public string? VnPayReturnUrlOverride { get; set; }

    /// <summary>
    /// (Tùy chọn) Return URL (redirectUrl) gửi lên MoMo — phải trỏ tới <c>/api/payments/momo/return</c> trên API.
    /// </summary>
    public string? MoMoReturnUrlOverride { get; set; }

    /// <summary>
    /// (Tùy chọn) IPN (ipnUrl) cho MoMo — phải trỏ tới <c>/api/payments/momo/ipn</c> (môi trường local cần URL public kiểu ngrok).
    /// </summary>
    public string? MoMoNotifyUrlOverride { get; set; }
}

/// <summary>
/// DTO cho thanh toán gộp nhiều đơn hàng (multi-shop checkout) trong 1 giao dịch VNPay.
/// </summary>
public class CreateBatchPaymentDto
{
    public List<Guid> OrderIds { get; set; } = new();
    public string? ClientReturnSuccessUrl { get; set; }
    public string? ClientReturnFailureUrl { get; set; }
    public string? VnPayReturnUrlOverride { get; set; }
    public string? MoMoReturnUrlOverride { get; set; }
    public string? MoMoNotifyUrlOverride { get; set; }
}

public class CreatePaymentResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? PaymentUrl { get; set; }
    public Guid? PaymentId { get; set; }
}

public class VNPayReturnDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ResponseCode { get; set; }
    public Guid? OrderId { get; set; }
    public string? OrderCode { get; set; }
    public Guid? PaymentId { get; set; }
    public decimal Amount { get; set; }
}

public class MoMoReturnDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int ResultCode { get; set; }
    public Guid? OrderId { get; set; }
    public string? OrderCode { get; set; }
    public Guid? PaymentId { get; set; }
    public decimal Amount { get; set; }
}

public class MoMoIpnRequest
{
    public string PartnerCode { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string OrderInfo { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public long TransId { get; set; }
    public int ResultCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string PayType { get; set; } = string.Empty;
    public long ResponseTime { get; set; }
    public string ExtraData { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}
