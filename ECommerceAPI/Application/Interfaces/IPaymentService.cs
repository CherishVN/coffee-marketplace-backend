using ECommerceAPI.Application.DTOs.Payments;
using Microsoft.AspNetCore.Http;

namespace ECommerceAPI.Application.Interfaces;

public interface IPaymentService
{
    Task<CreatePaymentResponseDto> CreateVNPayPaymentAsync(Guid orderId, Guid customerId, string ipAddress);
    Task<VNPayReturnDto> ProcessVNPayReturnAsync(IQueryCollection queryParams);

    Task<CreatePaymentResponseDto> CreateMoMoPaymentAsync(Guid orderId, Guid customerId);
    Task<MoMoReturnDto> ProcessMoMoIpnAsync(MoMoIpnRequest request);
    Task<MoMoReturnDto> ProcessMoMoReturnAsync(IQueryCollection queryParams);
}
