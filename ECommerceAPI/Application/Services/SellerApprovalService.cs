using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class SellerApprovalService : ISellerApprovalService
{
    private readonly ApplicationDbContext _context;

    public SellerApprovalService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ShopListResponseDto> GetPendingShopsAsync(int page, int pageSize, short? verificationStatus)
    {
        var query = _context.Shops
            .Include(s => s.Owner)
            .Include(s => s.ShopDocuments)
            .Include(s => s.VerifiedByNavigation)
            .AsQueryable();

        if (verificationStatus.HasValue)
            query = query.Where(s => s.VerificationStatus == verificationStatus.Value);

        var totalCount = await query.CountAsync();

        var shops = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ShopListResponseDto
        {
            Success = true,
            Shops = shops.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ShopResponseDto> GetShopByIdAsync(Guid shopId)
    {
        var shop = await _context.Shops
            .Include(s => s.Owner)
            .Include(s => s.ShopDocuments)
                .ThenInclude(d => d.ReviewedByNavigation)
            .Include(s => s.VerifiedByNavigation)
            .FirstOrDefaultAsync(s => s.Id == shopId);

        if (shop == null)
        {
            return new ShopResponseDto
            {
                Success = false,
                Message = "Không tìm thấy shop"
            };
        }

        return new ShopResponseDto
        {
            Success = true,
            Shop = MapToDto(shop)
        };
    }

    public async Task<ShopResponseDto> ApproveShopAsync(Guid shopId, ApproveSellerDto dto, Guid adminId)
    {
        var shop = await _context.Shops
            .Include(s => s.Owner)
            .Include(s => s.ShopDocuments)
            .FirstOrDefaultAsync(s => s.Id == shopId);

        if (shop == null)
        {
            return new ShopResponseDto
            {
                Success = false,
                Message = "Không tìm thấy shop"
            };
        }

        if (shop.VerificationStatus != 0)
        {
            return new ShopResponseDto
            {
                Success = false,
                Message = "Chỉ có thể duyệt shop đang chờ xử lý"
            };
        }

        shop.VerificationStatus = 1;
        shop.Status = 1;
        shop.VerifiedAt = DateTime.UtcNow;
        shop.VerifiedBy = adminId;
        shop.RejectionReason = null;
        shop.UpdatedAt = DateTime.UtcNow;

        if (shop.Owner != null && shop.Owner.Role == "customer")
        {
            shop.Owner.Role = "seller";
            shop.Owner.UpdatedAt = DateTime.UtcNow;
        }

        foreach (var doc in shop.ShopDocuments.Where(d => d.Status == 0))
        {
            doc.Status = 1;
            doc.ReviewedAt = DateTime.UtcNow;
            doc.ReviewedBy = adminId;
        }

        var auditLog = new UserAuditLog
        {
            Id = Guid.NewGuid(),
            UserId = shop.OwnerId,
            EditorId = adminId,
            Action = "APPROVE_SELLER",
            FieldName = "Shop",
            OldValue = "VerificationStatus: Pending",
            NewValue = $"VerificationStatus: Approved - {dto.Note}",
            CreatedAt = DateTime.UtcNow
        };
        await _context.UserAuditLogs.AddAsync(auditLog);

        await _context.SaveChangesAsync();

        var updatedShop = await _context.Shops
            .Include(s => s.Owner)
            .Include(s => s.ShopDocuments)
            .Include(s => s.VerifiedByNavigation)
            .FirstOrDefaultAsync(s => s.Id == shopId);

        return new ShopResponseDto
        {
            Success = true,
            Message = "Đã duyệt shop thành công. Seller có thể bắt đầu bán hàng.",
            Shop = MapToDto(updatedShop!)
        };
    }

    public async Task<ShopResponseDto> RejectShopAsync(Guid shopId, RejectSellerDto dto, Guid adminId)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            return new ShopResponseDto
            {
                Success = false,
                Message = "Vui lòng nhập lý do từ chối"
            };
        }

        var shop = await _context.Shops
            .Include(s => s.Owner)
            .Include(s => s.ShopDocuments)
            .FirstOrDefaultAsync(s => s.Id == shopId);

        if (shop == null)
        {
            return new ShopResponseDto
            {
                Success = false,
                Message = "Không tìm thấy shop"
            };
        }

        if (shop.VerificationStatus != 0)
        {
            return new ShopResponseDto
            {
                Success = false,
                Message = "Chỉ có thể từ chối shop đang chờ xử lý"
            };
        }

        shop.VerificationStatus = 2;
        shop.RejectionReason = dto.Reason;
        shop.VerifiedAt = DateTime.UtcNow;
        shop.VerifiedBy = adminId;
        shop.UpdatedAt = DateTime.UtcNow;

        var auditLog = new UserAuditLog
        {
            Id = Guid.NewGuid(),
            UserId = shop.OwnerId,
            EditorId = adminId,
            Action = "REJECT_SELLER",
            FieldName = "Shop",
            OldValue = "VerificationStatus: Pending",
            NewValue = $"VerificationStatus: Rejected - {dto.Reason}",
            CreatedAt = DateTime.UtcNow
        };
        await _context.UserAuditLogs.AddAsync(auditLog);

        await _context.SaveChangesAsync();

        var updatedShop = await _context.Shops
            .Include(s => s.Owner)
            .Include(s => s.ShopDocuments)
            .Include(s => s.VerifiedByNavigation)
            .FirstOrDefaultAsync(s => s.Id == shopId);

        return new ShopResponseDto
        {
            Success = true,
            Message = "Đã từ chối shop. Seller có thể resubmit sau khi sửa thông tin.",
            Shop = MapToDto(updatedShop!)
        };
    }

    private static ShopVerificationDto MapToDto(Shop s)
    {
        return new ShopVerificationDto
        {
            Id = s.Id,
            OwnerId = s.OwnerId,
            OwnerName = s.Owner?.FullName,
            Name = s.Name,
            Slug = s.Slug,
            Description = s.Description,
            LogoUrl = s.LogoUrl,
            Status = s.Status,
            StatusName = GetStatusName(s.Status),
            VerificationStatus = s.VerificationStatus,
            VerificationStatusName = GetVerificationStatusName(s.VerificationStatus),
            RejectionReason = s.RejectionReason,
            VerifiedAt = s.VerifiedAt,
            VerifiedBy = s.VerifiedBy,
            VerifiedByName = s.VerifiedByNavigation?.FullName,
            CreatedAt = s.CreatedAt,
            Documents = s.ShopDocuments?.Select(d => new ShopDocumentDto
            {
                Id = d.Id,
                DocType = d.DocType,
                FileUrl = d.FileUrl,
                Status = d.Status,
                StatusName = GetDocStatusName(d.Status),
                RejectionReason = d.RejectionReason,
                SubmittedAt = d.SubmittedAt,
                ReviewedAt = d.ReviewedAt,
                ReviewedByName = d.ReviewedByNavigation?.FullName
            }).ToList() ?? new()
        };
    }

    public async Task<ShopResponseDto> ActivateShopAsync(Guid shopId, Guid adminId)
    {
        var shop = await _context.Shops
            .Include(s => s.Owner)
            .FirstOrDefaultAsync(s => s.Id == shopId);

        if (shop == null)
        {
            return new ShopResponseDto { Success = false, Message = "Không tìm thấy shop" };
        }

        if (shop.VerificationStatus != 1)
        {
            return new ShopResponseDto { Success = false, Message = "Shop chưa được xác minh, không thể kích hoạt" };
        }

        if (shop.Status == 1)
        {
            return new ShopResponseDto { Success = false, Message = "Shop đang ở trạng thái Active" };
        }

        var oldStatus = shop.Status;
        shop.Status = 1;
        shop.SuspensionReason = null;
        shop.SuspendedAt = null;
        shop.SuspendedBy = null;
        shop.UpdatedAt = DateTime.UtcNow;

        await _context.UserAuditLogs.AddAsync(new UserAuditLog
        {
            Id = Guid.NewGuid(),
            UserId = shop.OwnerId,
            EditorId = adminId,
            Action = "ACTIVATE_SHOP",
            FieldName = "Shop.Status",
            OldValue = GetStatusName(oldStatus),
            NewValue = "Active",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        var updatedShop = await _context.Shops
            .Include(s => s.Owner)
            .Include(s => s.VerifiedByNavigation)
            .FirstOrDefaultAsync(s => s.Id == shopId);

        return new ShopResponseDto
        {
            Success = true,
            Message = "Đã kích hoạt shop thành công",
            Shop = MapToDto(updatedShop!)
        };
    }

    public async Task<ShopResponseDto> SuspendShopAsync(Guid shopId, SuspendShopDto dto, Guid adminId)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            return new ShopResponseDto { Success = false, Message = "Vui lòng nhập lý do tạm ngưng" };
        }

        var shop = await _context.Shops
            .Include(s => s.Owner)
            .Include(s => s.Products)
            .FirstOrDefaultAsync(s => s.Id == shopId);

        if (shop == null)
        {
            return new ShopResponseDto { Success = false, Message = "Không tìm thấy shop" };
        }

        if (shop.Status != 1)
        {
            return new ShopResponseDto { Success = false, Message = "Chỉ có thể tạm ngưng shop đang Active" };
        }

        var activeOrdersCount = await _context.Orders
            .Where(o => o.ShopId == shopId && o.Status >= 0 && o.Status < 3)
            .CountAsync();

        var oldStatus = shop.Status;
        shop.Status = 2;
        shop.SuspensionReason = dto.Reason;
        shop.SuspendedAt = DateTime.UtcNow;
        shop.SuspendedBy = adminId;
        shop.UpdatedAt = DateTime.UtcNow;

        await _context.UserAuditLogs.AddAsync(new UserAuditLog
        {
            Id = Guid.NewGuid(),
            UserId = shop.OwnerId,
            EditorId = adminId,
            Action = "SUSPEND_SHOP",
            FieldName = "Shop.Status",
            OldValue = GetStatusName(oldStatus),
            NewValue = $"Suspended - {dto.Reason}",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        var updatedShop = await _context.Shops
            .Include(s => s.Owner)
            .Include(s => s.VerifiedByNavigation)
            .FirstOrDefaultAsync(s => s.Id == shopId);

        var message = activeOrdersCount > 0
            ? $"Đã tạm ngưng shop. Lưu ý: Shop có {activeOrdersCount} đơn hàng đang xử lý, cần tiếp tục hoàn thành."
            : "Đã tạm ngưng shop thành công. Sản phẩm sẽ bị ẩn khỏi tìm kiếm.";

        return new ShopResponseDto
        {
            Success = true,
            Message = message,
            Shop = MapToDto(updatedShop!)
        };
    }

    public async Task<ShopResponseDto> CloseShopAsync(Guid shopId, SuspendShopDto dto, Guid adminId)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            return new ShopResponseDto { Success = false, Message = "Vui lòng nhập lý do đóng shop" };
        }

        var shop = await _context.Shops
            .Include(s => s.Owner)
            .FirstOrDefaultAsync(s => s.Id == shopId);

        if (shop == null)
        {
            return new ShopResponseDto { Success = false, Message = "Không tìm thấy shop" };
        }

        if (shop.Status == 3)
        {
            return new ShopResponseDto { Success = false, Message = "Shop đã bị đóng trước đó" };
        }

        var activeOrdersCount = await _context.Orders
            .Where(o => o.ShopId == shopId && o.Status >= 0 && o.Status < 3)
            .CountAsync();

        if (activeOrdersCount > 0)
        {
            return new ShopResponseDto 
            { 
                Success = false, 
                Message = $"Không thể đóng shop vì còn {activeOrdersCount} đơn hàng đang xử lý. Vui lòng tạm ngưng (suspend) thay thế." 
            };
        }

        var oldStatus = shop.Status;
        shop.Status = 3;
        shop.SuspensionReason = dto.Reason;
        shop.SuspendedAt = DateTime.UtcNow;
        shop.SuspendedBy = adminId;
        shop.UpdatedAt = DateTime.UtcNow;

        await _context.UserAuditLogs.AddAsync(new UserAuditLog
        {
            Id = Guid.NewGuid(),
            UserId = shop.OwnerId,
            EditorId = adminId,
            Action = "CLOSE_SHOP",
            FieldName = "Shop.Status",
            OldValue = GetStatusName(oldStatus),
            NewValue = $"Closed - {dto.Reason}",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        var updatedShop = await _context.Shops
            .Include(s => s.Owner)
            .Include(s => s.VerifiedByNavigation)
            .FirstOrDefaultAsync(s => s.Id == shopId);

        return new ShopResponseDto
        {
            Success = true,
            Message = "Đã đóng shop vĩnh viễn",
            Shop = MapToDto(updatedShop!)
        };
    }

    private static string GetStatusName(short status)
    {
        return status switch
        {
            0 => "Pending",
            1 => "Active",
            2 => "Suspended",
            3 => "Closed",
            _ => "Unknown"
        };
    }

    private static string GetVerificationStatusName(short status)
    {
        return status switch
        {
            0 => "Pending",
            1 => "Approved",
            2 => "Rejected",
            _ => "Unknown"
        };
    }

    private static string GetDocStatusName(short status)
    {
        return status switch
        {
            0 => "Pending",
            1 => "Approved",
            2 => "Rejected",
            _ => "Unknown"
        };
    }
}
