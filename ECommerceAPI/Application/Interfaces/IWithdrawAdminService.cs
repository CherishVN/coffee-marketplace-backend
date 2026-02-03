using ECommerceAPI.Application.DTOs.Admin;

namespace ECommerceAPI.Application.Interfaces;

public interface IWithdrawAdminService
{
    Task<WithdrawListResponseDto> GetAllRequestsAsync(int page, int pageSize, short? status);
    Task<WithdrawResponseDto> GetRequestByIdAsync(Guid requestId);
    Task<WithdrawResponseDto> ApproveRequestAsync(Guid requestId, ApproveWithdrawDto dto, Guid adminId);
    Task<WithdrawResponseDto> RejectRequestAsync(Guid requestId, RejectWithdrawDto dto, Guid adminId);
}
