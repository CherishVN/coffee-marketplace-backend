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
    public Guid? PaymentId { get; set; }
    public decimal Amount { get; set; }
}
