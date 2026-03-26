using ECommerceAPI.Application.DTOs.Admin;

namespace ECommerceAPI.Application.Interfaces;

public interface IMaterialAdminService
{
    Task<MaterialListResponseDto> GetAllMaterialsAsync(int page, int pageSize, string? search = null, bool? isActive = null);
    Task<MaterialResponseDto> GetMaterialByIdAsync(Guid materialId);
    Task<MaterialResponseDto> CreateMaterialAsync(CreateMaterialDto dto, Guid adminId);
    Task<MaterialResponseDto> UpdateMaterialAsync(Guid materialId, UpdateMaterialDto dto, Guid adminId);
    Task<MaterialResponseDto> ToggleActiveAsync(Guid materialId, Guid adminId);
    Task<MaterialResponseDto> DeleteMaterialAsync(Guid materialId, Guid adminId);
}
