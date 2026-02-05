using ECommerceAPI.Application.DTOs.Admin;

namespace ECommerceAPI.Application.Interfaces;

public interface ITagAdminService
{
    Task<TagListResponseDto> GetAllTagsAsync(int page, int pageSize, string? search = null);
    Task<TagResponseDto> GetTagByIdAsync(long tagId);
    Task<TagResponseDto> CreateTagAsync(CreateTagDto dto, Guid adminId);
    Task<TagResponseDto> UpdateTagAsync(long tagId, UpdateTagDto dto, Guid adminId);
    Task<TagResponseDto> DeleteTagAsync(long tagId, Guid adminId);
}
