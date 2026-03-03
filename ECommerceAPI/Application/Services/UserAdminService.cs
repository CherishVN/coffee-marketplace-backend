using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class UserAdminService : IUserAdminService
{
    private readonly ApplicationDbContext _context;

    public UserAdminService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserListResponseDto> GetAllUsersAsync(int page, int pageSize, string? role, short? status)
    {
        IQueryable<User> query;

        if (!string.IsNullOrEmpty(role))
            query = _context.Users.FromSqlInterpolated($"SELECT * FROM users WHERE role = {role}::user_role");
        else
            query = _context.Users.AsQueryable();

        if (status.HasValue)
            query = query.Where(u => u.Status == status.Value);

        var totalCount = await query.CountAsync();

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Phone = u.Phone,
                Role = u.Role,
                Status = u.Status,
                StatusName = GetStatusName(u.Status),
                HasOrders = u.Orders.Any(),
                SuspensionReason = u.SuspensionReason,
                SuspendedAt = u.SuspendedAt,
                SuspendedBy = u.SuspendedBy,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            })
            .ToListAsync();

        return new UserListResponseDto
        {
            Success = true,
            Users = users,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<UserResponseDto> GetUserByIdAsync(Guid userId)
    {
        var user = await _context.Users
            .Include(u => u.Orders)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return new UserResponseDto
            {
                Success = false,
                Message = "Không tìm thấy user"
            };
        }

        return new UserResponseDto
        {
            Success = true,
            User = MapToAdminUserDto(user)
        };
    }

    public async Task<UserResponseDto> UpdateUserAsync(Guid userId, UpdateUserDto dto, Guid editorId)
    {
        var user = await _context.Users
            .Include(u => u.Orders)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return new UserResponseDto
            {
                Success = false,
                Message = "Không tìm thấy user"
            };
        }

        var auditLogs = new List<UserAuditLog>();

        if (dto.Phone != null && dto.Phone != user.Phone)
        {
            var phoneExists = await _context.Users.AnyAsync(u => u.Phone == dto.Phone && u.Id != userId);
            if (phoneExists)
            {
                return new UserResponseDto
                {
                    Success = false,
                    Message = "Số điện thoại đã được sử dụng"
                };
            }

            auditLogs.Add(CreateAuditLog(userId, editorId, "UPDATE", "Phone", user.Phone, dto.Phone));
            user.Phone = dto.Phone;
        }

        if (dto.FullName != null && dto.FullName != user.FullName)
        {
            auditLogs.Add(CreateAuditLog(userId, editorId, "UPDATE", "FullName", user.FullName, dto.FullName));
            user.FullName = dto.FullName;
        }

        if (dto.Role != null && dto.Role != user.Role)
        {
            var validRoles = new[] { "customer", "seller", "admin" };
            if (!validRoles.Contains(dto.Role))
            {
                return new UserResponseDto
                {
                    Success = false,
                    Message = "Role không hợp lệ"
                };
            }

            auditLogs.Add(CreateAuditLog(userId, editorId, "UPDATE", "Role", user.Role, dto.Role));
            user.Role = dto.Role;
        }

        user.UpdatedAt = DateTime.UtcNow;

        if (auditLogs.Any())
        {
            await _context.UserAuditLogs.AddRangeAsync(auditLogs);
        }

        await _context.SaveChangesAsync();

        return new UserResponseDto
        {
            Success = true,
            Message = "Cập nhật user thành công",
            User = MapToAdminUserDto(user)
        };
    }

    public async Task<UserResponseDto> SuspendUserAsync(Guid userId, SuspendUserDto dto, Guid adminId)
    {
        if (userId == adminId)
        {
            return new UserResponseDto
            {
                Success = false,
                Message = "Không thể khóa chính tài khoản của mình"
            };
        }

        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            return new UserResponseDto
            {
                Success = false,
                Message = "Vui lòng nhập lý do khóa tài khoản"
            };
        }

        var user = await _context.Users
            .Include(u => u.Orders)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return new UserResponseDto
            {
                Success = false,
                Message = "Không tìm thấy user"
            };
        }

        if (user.Status == 0)
        {
            return new UserResponseDto
            {
                Success = false,
                Message = "Tài khoản đã bị khóa trước đó"
            };
        }

        var oldStatus = user.Status;
        user.Status = 0;
        user.SuspensionReason = dto.Reason;
        user.SuspendedAt = DateTime.UtcNow;
        user.SuspendedBy = adminId;
        user.UpdatedAt = DateTime.UtcNow;

        var auditLog = CreateAuditLog(userId, adminId, "SUSPEND", "Status", oldStatus.ToString(), "0");
        auditLog.NewValue = $"Suspended: {dto.Reason}";
        await _context.UserAuditLogs.AddAsync(auditLog);

        await _context.SaveChangesAsync();

        return new UserResponseDto
        {
            Success = true,
            Message = "Đã khóa tài khoản user",
            User = MapToAdminUserDto(user)
        };
    }

    public async Task<UserResponseDto> UnsuspendUserAsync(Guid userId, Guid adminId)
    {
        var user = await _context.Users
            .Include(u => u.Orders)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return new UserResponseDto
            {
                Success = false,
                Message = "Không tìm thấy user"
            };
        }

        if (user.Status != 0)
        {
            return new UserResponseDto
            {
                Success = false,
                Message = "Tài khoản không bị khóa"
            };
        }

        var oldReason = user.SuspensionReason;
        user.Status = 1;
        user.SuspensionReason = null;
        user.SuspendedAt = null;
        user.SuspendedBy = null;
        user.UpdatedAt = DateTime.UtcNow;

        var auditLog = CreateAuditLog(userId, adminId, "UNSUSPEND", "Status", "0", "1");
        auditLog.OldValue = $"Was suspended: {oldReason}";
        await _context.UserAuditLogs.AddAsync(auditLog);

        await _context.SaveChangesAsync();

        return new UserResponseDto
        {
            Success = true,
            Message = "Đã mở khóa tài khoản user",
            User = MapToAdminUserDto(user)
        };
    }

    public async Task<AuditLogResponseDto> GetUserAuditLogsAsync(Guid userId)
    {
        var logs = await _context.UserAuditLogs
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new UserAuditLogDto
            {
                Id = l.Id,
                UserId = l.UserId,
                EditorId = l.EditorId,
                EditorName = l.Editor.FullName,
                Action = l.Action,
                FieldName = l.FieldName,
                OldValue = l.OldValue,
                NewValue = l.NewValue,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync();

        return new AuditLogResponseDto
        {
            Success = true,
            Logs = logs
        };
    }

    private AdminUserDto MapToAdminUserDto(User user)
    {
        return new AdminUserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Phone = user.Phone,
            Role = user.Role,
            Status = user.Status,
            StatusName = GetStatusName(user.Status),
            HasOrders = user.Orders?.Any() ?? false,
            SuspensionReason = user.SuspensionReason,
            SuspendedAt = user.SuspendedAt,
            SuspendedBy = user.SuspendedBy,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    private static string GetStatusName(short status)
    {
        return status switch
        {
            0 => "Suspended",
            1 => "Active",
            _ => "Unknown"
        };
    }

    private UserAuditLog CreateAuditLog(Guid userId, Guid editorId, string action, string fieldName, string? oldValue, string? newValue)
    {
        return new UserAuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EditorId = editorId,
            Action = action,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedAt = DateTime.UtcNow
        };
    }
}
