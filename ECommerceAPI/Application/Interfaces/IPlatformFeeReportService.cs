using ECommerceAPI.Application.DTOs.Admin;

namespace ECommerceAPI.Application.Interfaces;

public interface IPlatformFeeReportService
{
    Task<PlatformFeeSummaryDto> GetSummaryAsync(DateTime? fromUtc, DateTime? toUtc);

    Task<PlatformFeeRecordsListResponseDto> GetRecordsAsync(
        int page,
        int pageSize,
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? shopId);
}
