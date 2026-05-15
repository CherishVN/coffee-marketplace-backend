using ECommerceAPI.Application.DTOs.Admin;

namespace ECommerceAPI.Application.Interfaces;

public interface IDisputeAdminService
{
    Task<DisputeListResponseDto> GetAllDisputesAsync(
        int page, 
        int pageSize, 
        short? status = null,
        short? type = null,
        Guid? customerId = null);
        
    Task<DisputeResponseDto> GetDisputeByIdAsync(Guid disputeId);
    Task<DisputeResponseDto> ApproveRefundAsync(Guid disputeId, ApproveRefundDto dto, Guid adminId);
    Task<DisputeResponseDto> RejectDisputeAsync(Guid disputeId, RejectDisputeDto dto, Guid adminId);
    Task<DisputeResponseDto> RequestSellerResponseAsync(Guid disputeId, RequestResponseDto dto, Guid adminId);
    Task<DisputeResponseDto> RequestCustomerResponseAsync(Guid disputeId, RequestResponseDto dto, Guid adminId);
    Task<int> AutoRefundReturnedDisputesAsync(int days, CancellationToken cancellationToken = default);
}
