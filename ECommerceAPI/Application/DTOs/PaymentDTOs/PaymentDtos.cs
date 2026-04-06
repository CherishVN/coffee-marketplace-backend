namespace ECommerceAPI.Application.DTOs.Payments;

public class CreatePaymentDto
{
    public Guid OrderId { get; set; }
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
