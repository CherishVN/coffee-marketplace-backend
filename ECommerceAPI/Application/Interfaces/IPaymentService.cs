using ECommerceAPI.Application.DTOs.Payments;
using Microsoft.AspNetCore.Http;

namespace ECommerceAPI.Application.Interfaces;

public interface IPaymentService
{
    Task<CreatePaymentResponseDto> CreateVNPayPaymentAsync(
        Guid orderId,
        Guid customerId,
        string ipAddress,
        string? clientReturnSuccessUrl = null,
        string? clientReturnFailureUrl = null,
        string? vnPayReturnUrlOverride = null);
    Task<VNPayReturnDto> ProcessVNPayReturnAsync(IQueryCollection queryParams, string rawQueryString);

    Task<CreatePaymentResponseDto> CreateMoMoPaymentAsync(Guid orderId, Guid customerId);
    Task<MoMoReturnDto> ProcessMoMoIpnAsync(MoMoIpnRequest request);
    Task<MoMoReturnDto> ProcessMoMoReturnAsync(IQueryCollection queryParams);
    Task<int> ExpireStalePendingPaymentsAsync(CancellationToken cancellationToken = default);
}
