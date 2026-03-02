using ECommerceAPI.Application.DTOs.Disputes;

namespace ECommerceAPI.Application.Interfaces;

public interface ICustomerDisputeService
{
    Task<CustomerDisputeResponseDto> CreateDisputeAsync(Guid customerId, CreateDisputeDto dto);
    Task<CustomerDisputeResponseDto> UpdateEvidenceAsync(Guid customerId, Guid disputeId, UpdateEvidenceDto dto);
    Task<CustomerDisputeListResponseDto> GetMyDisputesAsync(Guid customerId, int page, int pageSize, short? status = null);
    Task<CustomerDisputeResponseDto> GetDisputeByIdAsync(Guid customerId, Guid disputeId);
    Task<CustomerDisputeResponseDto> CancelDisputeAsync(Guid customerId, Guid disputeId);
}
