using ECommerceAPI.Application.DTOs.Payments;
using Microsoft.AspNetCore.Http;

namespace ECommerceAPI.Application.Interfaces;

public interface IPaymentService
{
    Task<CreatePaymentResponseDto> CreateVNPayPaymentAsync(Guid orderId, Guid customerId, string ipAddress);
    Task<VNPayReturnDto> ProcessVNPayReturnAsync(IQueryCollection queryParams);
}
