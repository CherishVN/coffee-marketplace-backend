using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class MaterialAdminService : IMaterialAdminService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MaterialAdminService> _logger;

    public MaterialAdminService(ApplicationDbContext context, ILogger<MaterialAdminService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<MaterialListResponseDto> GetAllMaterialsAsync(int page, int pageSize, string? search = null, bool? isActive = null)
    {
        try
        {
            var query = _context.Materials.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(m => m.Name.Contains(search) || m.Slug.Contains(search));

            if (isActive.HasValue)
                query = query.Where(m => m.IsActive == isActive.Value);

            var totalCount = await query.CountAsync();

            var materials = await query
                .OrderBy(m => m.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MaterialDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Slug = m.Slug,
                    Description = m.Description,
                    IsActive = m.IsActive,
                    CreatedAt = m.CreatedAt,
                    ProductCount = m.ProductMaterials.Count
                })
                .ToListAsync();

            return new MaterialListResponseDto
            {
                Success = true,
                Materials = materials,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all materials");
            return new MaterialListResponseDto { Success = false, Message = "Có lỗi xảy ra khi lấy danh sách chất liệu" };
        }
    }

    public async Task<MaterialResponseDto> GetMaterialByIdAsync(Guid materialId)
    {
        try
        {
            var material = await _context.Materials
                .Where(m => m.Id == materialId)
                .Select(m => new MaterialDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Slug = m.Slug,
                    Description = m.Description,
                    IsActive = m.IsActive,
                    CreatedAt = m.CreatedAt,
                    ProductCount = m.ProductMaterials.Count
                })
                .FirstOrDefaultAsync();

            if (material == null)
                return new MaterialResponseDto { Success = false, Message = "Không tìm thấy chất liệu" };

            return new MaterialResponseDto { Success = true, Material = material };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting material {MaterialId}", materialId);
            return new MaterialResponseDto { Success = false, Message = "Có lỗi xảy ra khi lấy thông tin chất liệu" };
        }
    }

    public async Task<MaterialResponseDto> CreateMaterialAsync(CreateMaterialDto dto, Guid adminId)
    {
        try
        {
            var slug = GenerateSlug(dto.Name);

            if (await _context.Materials.AnyAsync(m => m.Slug == slug))
                return new MaterialResponseDto { Success = false, Message = "Chất liệu với tên này đã tồn tại" };

            var material = new Material
            {
                Id = Guid.NewGuid(),
                Name = dto.Name.Trim(),
                Slug = slug,
                Description = dto.Description?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Materials.Add(material);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Material created: {MaterialId} by admin: {AdminId}", material.Id, adminId);

            return new MaterialResponseDto
            {
                Success = true,
                Message = "Tạo chất liệu thành công",
                Material = new MaterialDto
                {
                    Id = material.Id,
                    Name = material.Name,
                    Slug = material.Slug,
                    Description = material.Description,
                    IsActive = material.IsActive,
                    CreatedAt = material.CreatedAt,
                    ProductCount = 0
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating material");
            return new MaterialResponseDto { Success = false, Message = "Có lỗi xảy ra khi tạo chất liệu" };
        }
    }

    public async Task<MaterialResponseDto> UpdateMaterialAsync(Guid materialId, UpdateMaterialDto dto, Guid adminId)
    {
        try
        {
            var material = await _context.Materials.FindAsync(materialId);
            if (material == null)
                return new MaterialResponseDto { Success = false, Message = "Không tìm thấy chất liệu" };

            var newSlug = GenerateSlug(dto.Name);

            if (await _context.Materials.AnyAsync(m => m.Slug == newSlug && m.Id != materialId))
                return new MaterialResponseDto { Success = false, Message = "Chất liệu với tên này đã tồn tại" };

            material.Name = dto.Name.Trim();
            material.Slug = newSlug;
            material.Description = dto.Description?.Trim();

            await _context.SaveChangesAsync();

            _logger.LogInformation("Material updated: {MaterialId} by admin: {AdminId}", materialId, adminId);

            return new MaterialResponseDto
            {
                Success = true,
                Message = "Cập nhật chất liệu thành công",
                Material = new MaterialDto
                {
                    Id = material.Id,
                    Name = material.Name,
                    Slug = material.Slug,
                    Description = material.Description,
                    IsActive = material.IsActive,
                    CreatedAt = material.CreatedAt
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating material {MaterialId}", materialId);
            return new MaterialResponseDto { Success = false, Message = "Có lỗi xảy ra khi cập nhật chất liệu" };
        }
    }

    public async Task<MaterialResponseDto> ToggleActiveAsync(Guid materialId, Guid adminId)
    {
        try
        {
            var material = await _context.Materials.FindAsync(materialId);
            if (material == null)
                return new MaterialResponseDto { Success = false, Message = "Không tìm thấy chất liệu" };

            material.IsActive = !material.IsActive;
            await _context.SaveChangesAsync();

            var action = material.IsActive ? "kích hoạt" : "vô hiệu hóa";
            _logger.LogInformation("Material {Action}: {MaterialId} by admin: {AdminId}", action, materialId, adminId);

            return new MaterialResponseDto
            {
                Success = true,
                Message = $"Đã {action} chất liệu thành công",
                Material = new MaterialDto
                {
                    Id = material.Id,
                    Name = material.Name,
                    Slug = material.Slug,
                    Description = material.Description,
                    IsActive = material.IsActive,
                    CreatedAt = material.CreatedAt
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling material {MaterialId}", materialId);
            return new MaterialResponseDto { Success = false, Message = "Có lỗi xảy ra khi thay đổi trạng thái chất liệu" };
        }
    }

    public async Task<MaterialResponseDto> DeleteMaterialAsync(Guid materialId, Guid adminId)
    {
        try
        {
            var material = await _context.Materials
                .Include(m => m.ProductMaterials)
                .FirstOrDefaultAsync(m => m.Id == materialId);

            if (material == null)
                return new MaterialResponseDto { Success = false, Message = "Không tìm thấy chất liệu" };

            if (material.ProductMaterials.Any())
                return new MaterialResponseDto
                {
                    Success = false,
                    Message = $"Không thể xóa chất liệu vì đang được sử dụng bởi {material.ProductMaterials.Count} sản phẩm. Hãy vô hiệu hóa thay vì xóa."
                };

            _context.Materials.Remove(material);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Material deleted: {MaterialId} by admin: {AdminId}", materialId, adminId);

            return new MaterialResponseDto { Success = true, Message = "Xóa chất liệu thành công" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting material {MaterialId}", materialId);
            return new MaterialResponseDto { Success = false, Message = "Có lỗi xảy ra khi xóa chất liệu" };
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static string GenerateSlug(string name)
    {
        var slug = name.ToLowerInvariant().Trim()
            .Replace("à", "a").Replace("á", "a").Replace("ả", "a").Replace("ã", "a").Replace("ạ", "a")
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

        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9]+", "-");
        return slug.Trim('-');
    }
}
