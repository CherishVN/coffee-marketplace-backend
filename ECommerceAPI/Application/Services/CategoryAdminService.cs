using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ECommerceAPI.Application.Services;

public class CategoryAdminService : ICategoryAdminService
{
    private readonly ApplicationDbContext _context;

    public CategoryAdminService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CategoryListResponseDto> GetAllCategoriesAsync(int page, int pageSize, short? level, bool? isActive)
    {
        var query = _context.Categories
            .Include(c => c.Parent)
            .Include(c => c.Products)
            .Include(c => c.InverseParent)
            .AsQueryable();

        if (level.HasValue)
            query = query.Where(c => c.Level == level.Value);

        if (isActive.HasValue)
            query = query.Where(c => c.IsActive == isActive.Value);

        var totalCount = await query.CountAsync();

        var categories = await query
            .OrderBy(c => c.Level)
            .ThenBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new CategoryListResponseDto
        {
            Success = true,
            Categories = categories.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CategoryTreeResponseDto> GetCategoryTreeAsync()
    {
        var allCategories = await _context.Categories
            .Include(c => c.Products)
            .Include(c => c.InverseParent)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var mainCategories = allCategories
            .Where(c => c.ParentId == null)
            .Select(c => MapToDtoWithChildren(c, allCategories))
            .ToList();

        return new CategoryTreeResponseDto
        {
            Success = true,
            Tree = mainCategories
        };
    }

    public async Task<CategoryResponseDto> GetCategoryByIdAsync(long categoryId)
    {
        var category = await _context.Categories
            .Include(c => c.Parent)
            .Include(c => c.Products)
            .Include(c => c.InverseParent)
            .FirstOrDefaultAsync(c => c.Id == categoryId);

        if (category == null)
        {
            return new CategoryResponseDto
            {
                Success = false,
                Message = "Không tìm thấy danh mục"
            };
        }

        return new CategoryResponseDto
        {
            Success = true,
            Category = MapToDto(category)
        };
    }

    public async Task<CategoryResponseDto> CreateCategoryAsync(CreateCategoryDto dto, Guid adminId)
    {
        short level = 1;
        Category? parent = null;

        if (dto.ParentId.HasValue)
        {
            parent = await _context.Categories.FirstOrDefaultAsync(c => c.Id == dto.ParentId.Value);
            if (parent == null)
            {
                return new CategoryResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy danh mục cha"
                };
            }

            if (parent.Level >= 2)
            {
                return new CategoryResponseDto
                {
                    Success = false,
                    Message = "Chỉ hỗ trợ tối đa 2 cấp danh mục (danh mục chính và danh mục con)"
                };
            }

            level = (short)(parent.Level + 1);
        }

        var codeExists = await _context.Categories
            .AnyAsync(c => c.Code == dto.Code && c.Level == level);
        if (codeExists)
        {
            return new CategoryResponseDto
            {
                Success = false,
                Message = $"Mã danh mục '{dto.Code}' đã tồn tại ở cấp {level}"
            };
        }

        var nameExists = await _context.Categories
            .AnyAsync(c => c.Name == dto.Name && c.Level == level);
        if (nameExists)
        {
            return new CategoryResponseDto
            {
                Success = false,
                Message = $"Tên danh mục '{dto.Name}' đã tồn tại ở cấp {level}"
            };
        }

        var slug = GenerateSlug(dto.Name);
        var slugExists = await _context.Categories.AnyAsync(c => c.Slug == slug);
        if (slugExists)
        {
            slug = $"{slug}-{DateTime.UtcNow.Ticks}";
        }

        var category = new Category
        {
            ParentId = dto.ParentId,
            Code = dto.Code,
            Name = dto.Name,
            Slug = slug,
            Level = level,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Categories.Add(category);

        await _context.UserAuditLogs.AddAsync(new UserAuditLog
        {
            Id = Guid.NewGuid(),
            UserId = adminId,
            EditorId = adminId,
            Action = "CREATE_CATEGORY",
            FieldName = "Category",
            OldValue = null,
            NewValue = $"Created: {dto.Code} - {dto.Name} (Level {level})",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        var createdCategory = await _context.Categories
            .Include(c => c.Parent)
            .Include(c => c.Products)
            .Include(c => c.InverseParent)
            .FirstOrDefaultAsync(c => c.Id == category.Id);

        return new CategoryResponseDto
        {
            Success = true,
            Message = "Tạo danh mục thành công",
            Category = MapToDto(createdCategory!)
        };
    }

    public async Task<CategoryResponseDto> UpdateCategoryAsync(long categoryId, UpdateCategoryDto dto, Guid adminId)
    {
        var category = await _context.Categories
            .Include(c => c.Parent)
            .Include(c => c.Products)
            .Include(c => c.InverseParent)
            .FirstOrDefaultAsync(c => c.Id == categoryId);

        if (category == null)
        {
            return new CategoryResponseDto
            {
                Success = false,
                Message = "Không tìm thấy danh mục"
            };
        }

        var changes = new List<string>();

        if (!string.IsNullOrWhiteSpace(dto.Code) && dto.Code != category.Code)
        {
            var codeExists = await _context.Categories
                .AnyAsync(c => c.Code == dto.Code && c.Level == category.Level && c.Id != categoryId);
            if (codeExists)
            {
                return new CategoryResponseDto
                {
                    Success = false,
                    Message = $"Mã danh mục '{dto.Code}' đã tồn tại ở cấp {category.Level}"
                };
            }
            changes.Add($"Code: {category.Code} → {dto.Code}");
            category.Code = dto.Code;
        }

        if (!string.IsNullOrWhiteSpace(dto.Name) && dto.Name != category.Name)
        {
            var nameExists = await _context.Categories
                .AnyAsync(c => c.Name == dto.Name && c.Level == category.Level && c.Id != categoryId);
            if (nameExists)
            {
                return new CategoryResponseDto
                {
                    Success = false,
                    Message = $"Tên danh mục '{dto.Name}' đã tồn tại ở cấp {category.Level}"
                };
            }
            changes.Add($"Name: {category.Name} → {dto.Name}");
            category.Name = dto.Name;

            var newSlug = GenerateSlug(dto.Name);
            var slugExists = await _context.Categories.AnyAsync(c => c.Slug == newSlug && c.Id != categoryId);
            if (slugExists)
            {
                newSlug = $"{newSlug}-{DateTime.UtcNow.Ticks}";
            }
            category.Slug = newSlug;
        }

        if (dto.ParentId.HasValue && dto.ParentId != category.ParentId)
        {
            if (dto.ParentId == categoryId)
            {
                return new CategoryResponseDto
                {
                    Success = false,
                    Message = "Danh mục không thể là cha của chính nó"
                };
            }

            var newParent = await _context.Categories.FirstOrDefaultAsync(c => c.Id == dto.ParentId.Value);
            if (newParent == null)
            {
                return new CategoryResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy danh mục cha mới"
                };
            }

            if (newParent.Level >= 2)
            {
                return new CategoryResponseDto
                {
                    Success = false,
                    Message = "Chỉ hỗ trợ tối đa 2 cấp danh mục"
                };
            }

            if (category.InverseParent.Any())
            {
                return new CategoryResponseDto
                {
                    Success = false,
                    Message = "Không thể di chuyển danh mục có danh mục con"
                };
            }

            changes.Add($"ParentId: {category.ParentId} → {dto.ParentId}");
            category.ParentId = dto.ParentId;
            category.Level = (short)(newParent.Level + 1);
        }
        else if (dto.ParentId == null && category.ParentId != null)
        {
            if (category.InverseParent.Any())
            {
                return new CategoryResponseDto
                {
                    Success = false,
                    Message = "Không thể chuyển danh mục con có subcategory thành danh mục chính"
                };
            }
            changes.Add($"ParentId: {category.ParentId} → null (promoted to main category)");
            category.ParentId = null;
            category.Level = 1;
        }

        if (!changes.Any())
        {
            return new CategoryResponseDto
            {
                Success = false,
                Message = "Không có thay đổi nào được thực hiện"
            };
        }

        category.UpdatedAt = DateTime.UtcNow;

        await _context.UserAuditLogs.AddAsync(new UserAuditLog
        {
            Id = Guid.NewGuid(),
            UserId = adminId,
            EditorId = adminId,
            Action = "UPDATE_CATEGORY",
            FieldName = "Category",
            OldValue = $"CategoryId: {categoryId}",
            NewValue = string.Join("; ", changes),
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        var updatedCategory = await _context.Categories
            .Include(c => c.Parent)
            .Include(c => c.Products)
            .Include(c => c.InverseParent)
            .FirstOrDefaultAsync(c => c.Id == categoryId);

        return new CategoryResponseDto
        {
            Success = true,
            Message = "Cập nhật danh mục thành công. Sản phẩm hiện có vẫn giữ nguyên danh mục.",
            Category = MapToDto(updatedCategory!)
        };
    }

    public async Task<CategoryResponseDto> ActivateCategoryAsync(long categoryId, Guid adminId)
    {
        var category = await _context.Categories
            .Include(c => c.Parent)
            .Include(c => c.Products)
            .Include(c => c.InverseParent)
            .FirstOrDefaultAsync(c => c.Id == categoryId);

        if (category == null)
        {
            return new CategoryResponseDto
            {
                Success = false,
                Message = "Không tìm thấy danh mục"
            };
        }

        if (category.IsActive)
        {
            return new CategoryResponseDto
            {
                Success = false,
                Message = "Danh mục đang ở trạng thái kích hoạt"
            };
        }

        if (category.ParentId.HasValue)
        {
            var parent = await _context.Categories.FirstOrDefaultAsync(c => c.Id == category.ParentId);
            if (parent != null && !parent.IsActive)
            {
                return new CategoryResponseDto
                {
                    Success = false,
                    Message = "Không thể kích hoạt danh mục con khi danh mục cha đang bị vô hiệu hóa"
                };
            }
        }

        category.IsActive = true;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.UserAuditLogs.AddAsync(new UserAuditLog
        {
            Id = Guid.NewGuid(),
            UserId = adminId,
            EditorId = adminId,
            Action = "ACTIVATE_CATEGORY",
            FieldName = "Category.IsActive",
            OldValue = "false",
            NewValue = "true",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return new CategoryResponseDto
        {
            Success = true,
            Message = "Đã kích hoạt danh mục. Danh mục sẽ xuất hiện trong gợi ý AI và kết quả tìm kiếm.",
            Category = MapToDto(category)
        };
    }

    public async Task<CategoryResponseDto> DeactivateCategoryAsync(long categoryId, ToggleCategoryStatusDto dto, Guid adminId)
    {
        var category = await _context.Categories
            .Include(c => c.Parent)
            .Include(c => c.Products)
            .Include(c => c.InverseParent)
            .FirstOrDefaultAsync(c => c.Id == categoryId);

        if (category == null)
        {
            return new CategoryResponseDto
            {
                Success = false,
                Message = "Không tìm thấy danh mục"
            };
        }

        if (!category.IsActive)
        {
            return new CategoryResponseDto
            {
                Success = false,
                Message = "Danh mục đang ở trạng thái vô hiệu hóa"
            };
        }

        category.IsActive = false;
        category.UpdatedAt = DateTime.UtcNow;

        if (category.InverseParent.Any())
        {
            foreach (var child in category.InverseParent)
            {
                child.IsActive = false;
                child.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.UserAuditLogs.AddAsync(new UserAuditLog
        {
            Id = Guid.NewGuid(),
            UserId = adminId,
            EditorId = adminId,
            Action = "DEACTIVATE_CATEGORY",
            FieldName = "Category.IsActive",
            OldValue = "true",
            NewValue = $"false - {dto.Reason ?? "No reason provided"}",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        var productCount = category.Products?.Count ?? 0;
        var subcategoryCount = category.InverseParent?.Count ?? 0;
        var message = "Đã vô hiệu hóa danh mục.";
        if (productCount > 0)
            message += $" {productCount} sản phẩm vẫn giữ danh mục này nhưng sẽ không xuất hiện trong tìm kiếm theo danh mục.";
        if (subcategoryCount > 0)
            message += $" {subcategoryCount} danh mục con cũng đã bị vô hiệu hóa.";

        return new CategoryResponseDto
        {
            Success = true,
            Message = message,
            Category = MapToDto(category)
        };
    }

    public async Task<CategoryResponseDto> DeleteCategoryAsync(long categoryId, Guid adminId)
    {
        var category = await _context.Categories
            .Include(c => c.Products)
            .Include(c => c.InverseParent)
            .FirstOrDefaultAsync(c => c.Id == categoryId);

        if (category == null)
        {
            return new CategoryResponseDto
            {
                Success = false,
                Message = "Không tìm thấy danh mục"
            };
        }

        if (category.Products.Any())
        {
            return new CategoryResponseDto
            {
                Success = false,
                Message = $"Không thể xóa danh mục vì có {category.Products.Count} sản phẩm đang sử dụng. Vui lòng vô hiệu hóa thay vì xóa."
            };
        }

        if (category.InverseParent.Any())
        {
            return new CategoryResponseDto
            {
                Success = false,
                Message = $"Không thể xóa danh mục vì có {category.InverseParent.Count} danh mục con. Vui lòng xóa danh mục con trước."
            };
        }

        var categoryInfo = $"{category.Code} - {category.Name}";

        await _context.UserAuditLogs.AddAsync(new UserAuditLog
        {
            Id = Guid.NewGuid(),
            UserId = adminId,
            EditorId = adminId,
            Action = "DELETE_CATEGORY",
            FieldName = "Category",
            OldValue = categoryInfo,
            NewValue = "Deleted",
            CreatedAt = DateTime.UtcNow
        });

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        return new CategoryResponseDto
        {
            Success = true,
            Message = "Đã xóa danh mục thành công"
        };
    }

    public async Task<MigrateProductsResponseDto> MigrateProductsAsync(long sourceCategoryId, MigrateProductsDto dto, Guid adminId)
    {
        var sourceCategory = await _context.Categories
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == sourceCategoryId);

        if (sourceCategory == null)
        {
            return new MigrateProductsResponseDto
            {
                Success = false,
                Message = "Không tìm thấy danh mục nguồn"
            };
        }

        var targetCategory = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == dto.TargetCategoryId);

        if (targetCategory == null)
        {
            return new MigrateProductsResponseDto
            {
                Success = false,
                Message = "Không tìm thấy danh mục đích"
            };
        }

        if (!targetCategory.IsActive)
        {
            return new MigrateProductsResponseDto
            {
                Success = false,
                Message = "Không thể di chuyển sản phẩm sang danh mục đã bị vô hiệu hóa"
            };
        }

        if (sourceCategoryId == dto.TargetCategoryId)
        {
            return new MigrateProductsResponseDto
            {
                Success = false,
                Message = "Danh mục nguồn và đích không được trùng nhau"
            };
        }

        var productCount = sourceCategory.Products.Count;
        if (productCount == 0)
        {
            return new MigrateProductsResponseDto
            {
                Success = false,
                Message = "Không có sản phẩm nào để di chuyển"
            };
        }

        foreach (var product in sourceCategory.Products)
        {
            product.CategoryId = dto.TargetCategoryId;
            product.UpdatedAt = DateTime.UtcNow;
        }

        await _context.UserAuditLogs.AddAsync(new UserAuditLog
        {
            Id = Guid.NewGuid(),
            UserId = adminId,
            EditorId = adminId,
            Action = "MIGRATE_PRODUCTS",
            FieldName = "Category.Products",
            OldValue = $"From: {sourceCategory.Code} - {sourceCategory.Name}",
            NewValue = $"To: {targetCategory.Code} - {targetCategory.Name} ({productCount} products)",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return new MigrateProductsResponseDto
        {
            Success = true,
            Message = $"Đã di chuyển {productCount} sản phẩm từ '{sourceCategory.Name}' sang '{targetCategory.Name}'",
            MigratedCount = productCount
        };
    }

    private CategoryDto MapToDto(Category c)
    {
        return new CategoryDto
        {
            Id = c.Id,
            ParentId = c.ParentId,
            ParentName = c.Parent?.Name,
            Code = c.Code,
            Name = c.Name,
            Slug = c.Slug,
            Level = c.Level,
            LevelName = c.Level.ToString(),
            IsActive = c.IsActive,
            ProductCount = c.Products?.Count ?? 0,
            SubcategoryCount = c.InverseParent?.Count ?? 0,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };
    }

    private CategoryDto MapToDtoWithChildren(Category c, List<Category> allCategories)
    {
        var dto = MapToDto(c);
        dto.Subcategories = allCategories
            .Where(sub => sub.ParentId == c.Id)
            .Select(sub => MapToDtoWithChildren(sub, allCategories))
            .ToList();
        return dto;
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.ToLowerInvariant();
        slug = RemoveVietnameseDiacritics(slug);
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        slug = slug.Trim('-');
        return slug;
    }

    private static string RemoveVietnameseDiacritics(string text)
    {
        string[] vietnameseSigns = new string[]
        {
            "aAeEoOuUiIdDyY",
            "áàạảãâấầậẩẫăắằặẳẵ",
            "ÁÀẠẢÃÂẤẦẬẨẪĂẮẰẶẲẴ",
            "éèẹẻẽêếềệểễ",
            "ÉÈẸẺẼÊẾỀỆỂỄ",
            "óòọỏõôốồộổỗơớờợởỡ",
            "ÓÒỌỎÕÔỐỒỘỔỖƠỚỜỢỞỠ",
            "úùụủũưứừựửữ",
            "ÚÙỤỦŨƯỨỪỰỬỮ",
            "íìịỉĩ",
            "ÍÌỊỈĨ",
            "đ",
            "Đ",
            "ýỳỵỷỹ",
            "ÝỲỴỶỸ"
        };

        for (int i = 1; i < vietnameseSigns.Length; i++)
        {
            for (int j = 0; j < vietnameseSigns[i].Length; j++)
            {
                text = text.Replace(vietnameseSigns[i][j], vietnameseSigns[0][i - 1]);
            }
        }
        return text;
    }
}
