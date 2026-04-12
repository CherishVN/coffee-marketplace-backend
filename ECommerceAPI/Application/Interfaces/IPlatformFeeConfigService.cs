using ECommerceAPI.Application.DTOs.Admin;

namespace ECommerceAPI.Application.Interfaces;

public interface IPlatformFeeConfigService
{
    /// <summary>Lấy cấu hình phí sàn đang áp dụng (bản ghi mới nhất trong DB).</summary>
    Task<PlatformFeeConfigDto?> GetCurrentAsync();

    /// <summary>Lấy tỷ lệ phần trăm phí sàn hiện tại. Fallback về appsettings nếu DB chưa có bản ghi.</summary>
    Task<decimal> GetCurrentCommissionPercentAsync();

    /// <summary>Cập nhật tỷ lệ phí sàn, lưu lịch sử thay đổi.</summary>
    Task<PlatformFeeConfigDto> UpdateAsync(UpdatePlatformFeeConfigRequest request, Guid adminId);

    /// <summary>Danh sách lịch sử thay đổi phí sàn (phân trang).</summary>
    Task<(List<PlatformFeeConfigDto> Items, int TotalCount)> GetHistoryAsync(int page, int pageSize);
}
