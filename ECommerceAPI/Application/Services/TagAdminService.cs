using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class TagAdminService : ITagAdminService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TagAdminService> _logger;

    public TagAdminService(
        ApplicationDbContext context,
        ILogger<TagAdminService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TagListResponseDto> GetAllTagsAsync(int page, int pageSize, string? search = null, bool? isActive = null)
    {
        try
        {
            var query = _context.Tags.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search}%";
                query = query.Where(t => EF.Functions.ILike(t.Name, pattern) || EF.Functions.ILike(t.Slug, pattern));
            }

            if (isActive.HasValue)
                query = query.Where(t => t.IsActive == isActive.Value);

            var totalCount = await query.CountAsync();

            var tags = await query
                .OrderBy(t => t.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TagDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Slug = t.Slug,
                    IsActive = t.IsActive,
                    CreatedAt = t.CreatedAt,
                    ProductCount = t.ProductTags.Count
                })
                .ToListAsync();

            return new TagListResponseDto
            {
                Success = true,
                Tags = tags,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all tags");
            return new TagListResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi lấy danh sách tag"
            };
        }
    }

    public async Task<TagResponseDto> GetTagByIdAsync(long tagId)
    {
        try
        {
            var tag = await _context.Tags
                .Where(t => t.Id == tagId)
                .Select(t => new TagDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Slug = t.Slug,
                    IsActive = t.IsActive,
                    CreatedAt = t.CreatedAt,
                    ProductCount = t.ProductTags.Count
                })
                .FirstOrDefaultAsync();

            if (tag == null)
            {
                return new TagResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy tag"
                };
            }

            return new TagResponseDto
            {
                Success = true,
                Tag = tag
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tag by id: {TagId}", tagId);
            return new TagResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi lấy thông tin tag"
            };
        }
    }

    public async Task<TagResponseDto> CreateTagAsync(CreateTagDto dto, Guid adminId)
    {
        try
        {
            // Generate slug from name
            var slug = GenerateSlug(dto.Name);

            // Check if slug already exists
            var existingTag = await _context.Tags
                .FirstOrDefaultAsync(t => t.Slug == slug);

            if (existingTag != null)
            {
                return new TagResponseDto
                {
                    Success = false,
                    Message = "Tag với tên này đã tồn tại"
                };
            }

            var tag = new Domain.Entities.Tag
            {
                Name = dto.Name.Trim(),
                Slug = slug,
                IsActive = dto.IsActive ?? true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Tags.Add(tag);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Tag created: {TagId} by admin: {AdminId}", tag.Id, adminId);

            return new TagResponseDto
            {
                Success = true,
                Message = "Tạo tag thành công",
                Tag = new TagDto
                {
                    Id = tag.Id,
                    Name = tag.Name,
                    Slug = tag.Slug,
                    IsActive = tag.IsActive,
                    CreatedAt = tag.CreatedAt,
                    ProductCount = 0
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tag");
            return new TagResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi tạo tag"
            };
        }
    }

    public async Task<TagResponseDto> UpdateTagAsync(long tagId, UpdateTagDto dto, Guid adminId)
    {
        try
        {
            var tag = await _context.Tags.FindAsync(tagId);

            if (tag == null)
            {
                return new TagResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy tag"
                };
            }

            var newSlug = GenerateSlug(dto.Name);

            // Check if new slug conflicts with another tag
            var conflictingTag = await _context.Tags
                .Where(t => t.Slug == newSlug && t.Id != tagId)
                .FirstOrDefaultAsync();

            if (conflictingTag != null)
            {
                return new TagResponseDto
                {
                    Success = false,
                    Message = "Tag với tên này đã tồn tại"
                };
            }

            tag.Name = dto.Name.Trim();
            tag.Slug = newSlug;
            if (dto.IsActive.HasValue)
                tag.IsActive = dto.IsActive.Value;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Tag updated: {TagId} by admin: {AdminId}", tagId, adminId);

            return new TagResponseDto
            {
                Success = true,
                Message = "Cập nhật tag thành công",
                Tag = new TagDto
                {
                    Id = tag.Id,
                    Name = tag.Name,
                    Slug = tag.Slug,
                    IsActive = tag.IsActive,
                    CreatedAt = tag.CreatedAt,
                    ProductCount = tag.ProductTags.Count
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tag: {TagId}", tagId);
            return new TagResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi cập nhật tag"
            };
        }
    }

    public async Task<TagResponseDto> DeleteTagAsync(long tagId, Guid adminId)
    {
        try
        {
            var tag = await _context.Tags
                .Include(t => t.ProductTags)
                .FirstOrDefaultAsync(t => t.Id == tagId);

            if (tag == null)
            {
                return new TagResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy tag"
                };
            }

            // Check if tag is being used
            if (tag.ProductTags.Any())
            {
                return new TagResponseDto
                {
                    Success = false,
                    Message = $"Không thể xóa tag vì đang được sử dụng bởi {tag.ProductTags.Count} sản phẩm"
                };
            }

            _context.Tags.Remove(tag);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Tag deleted: {TagId} by admin: {AdminId}", tagId, adminId);

            return new TagResponseDto
            {
                Success = true,
                Message = "Xóa tag thành công"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tag: {TagId}", tagId);
            return new TagResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi xóa tag"
            };
        }
    }

    private string GenerateSlug(string name)
    {
        // Convert to lowercase
        var slug = name.ToLowerInvariant().Trim();

        // Replace Vietnamese characters
        slug = slug.Replace("à", "a").Replace("á", "a").Replace("ả", "a").Replace("ã", "a").Replace("ạ", "a")
                   .Replace("ă", "a").Replace("ằ", "a").Replace("ắ", "a").Replace("ẳ", "a").Replace("ẵ", "a").Replace("ặ", "a")
                   .Replace("â", "a").Replace("ầ", "a").Replace("ấ", "a").Replace("ẩ", "a").Replace("ẫ", "a").Replace("ậ", "a")
                   .Replace("đ", "d")
                   .Replace("è", "e").Replace("é", "e").Replace("ẻ", "e").Replace("ẽ", "e").Replace("ẹ", "e")
                   .Replace("ê", "e").Replace("ề", "e").Replace("ế", "e").Replace("ể", "e").Replace("ễ", "e").Replace("ệ", "e")
                   .Replace("ì", "i").Replace("í", "i").Replace("ỉ", "i").Replace("ĩ", "i").Replace("ị", "i")
                   .Replace("ò", "o").Replace("ó", "o").Replace("ỏ", "o").Replace("õ", "o").Replace("ọ", "o")
                   .Replace("ô", "o").Replace("ồ", "o").Replace("ố", "o").Replace("ổ", "o").Replace("ỗ", "o").Replace("ộ", "o")
                   .Replace("ơ", "o").Replace("ờ", "o").Replace("ớ", "o").Replace("ở", "o").Replace("ỡ", "o").Replace("ợ", "o")
                   .Replace("ù", "u").Replace("ú", "u").Replace("ủ", "u").Replace("ũ", "u").Replace("ụ", "u")
                   .Replace("ư", "u").Replace("ừ", "u").Replace("ứ", "u").Replace("ử", "u").Replace("ữ", "u").Replace("ự", "u")
                   .Replace("ỳ", "y").Replace("ý", "y").Replace("ỷ", "y").Replace("ỹ", "y").Replace("ỵ", "y");

        // Replace spaces and special characters with hyphens
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9]+", "-");

        // Remove leading/trailing hyphens
        slug = slug.Trim('-');

        return slug;
    }
}
