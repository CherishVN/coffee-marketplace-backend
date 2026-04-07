using ECommerceAPI.Application.DTOs.Customer;

namespace ECommerceAPI.Application.Interfaces;

public interface ICustomerWalletService
{
    // Customer-facing
    Task<CustomerWalletDto?> GetWalletAsync(Guid customerId);
    Task<CustomerWalletLedgerResponseDto> GetTransactionsAsync(Guid customerId, int page, int pageSize);
    Task CreditRefundAsync(Guid customerId, decimal amount, string referenceType, Guid referenceId, string note);
    Task<CustomerWithdrawalResponseDto> CreateWithdrawalRequestAsync(Guid customerId, CreateCustomerWithdrawalDto dto);
    Task<CustomerWithdrawalListResponseDto> GetWithdrawalRequestsAsync(Guid customerId, int page, int pageSize);

    // Admin-facing
    Task<CustomerWithdrawalListResponseDto> AdminGetAllRequestsAsync(int page, int pageSize, short? status);
    Task<CustomerWithdrawalResponseDto> AdminApproveRequestAsync(Guid requestId, string? adminNote, Guid adminId);
    Task<CustomerWithdrawalResponseDto> AdminRejectRequestAsync(Guid requestId, string reason, string? adminNote, Guid adminId);
}
