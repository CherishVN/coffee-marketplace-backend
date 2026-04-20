using System.Net.Http.Json;
using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class UserAdminService : IUserAdminService
{
    private readonly ApplicationDbContext _context;
    private readonly IUserAuthEmailResolver _authResolver;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserAdminService> _logger;

    public UserAdminService(
        ApplicationDbContext context,
        IUserAuthEmailResolver authResolver,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<UserAdminService> logger)
    {
        _context = context;
        _authResolver = authResolver;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<UserListResponseDto> GetAllUsersAsync(int page, int pageSize, string? role, short? status)
    {
        IQueryable<User> query = _context.Users.AsQueryable();

        if (!string.IsNullOrEmpty(role))
            query = query.Where(u => u.Role != null && u.Role.Code == role);

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
                UserCode = u.UserCode,
                FullName = u.FullName,
                Phone = u.Phone,
                Role = u.Role != null ? u.Role.Code : string.Empty,
                Status = u.Status,
                StatusName = string.Empty,
                HasOrders = u.Orders.Any(),
                SuspensionReason = u.SuspensionReason,
                SuspendedAt = u.SuspendedAt,
                SuspendedBy = u.SuspendedBy,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            })
            .ToListAsync();

        foreach (var u in users)
            u.StatusName = GetStatusDisplayName(u.Status, u.SuspensionReason);

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
            .Include(u => u.Role)
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

        var dto = MapToAdminUserDto(user);
        await ApplySupabaseAuthToDtoAsync(dto, userId);

        return new UserResponseDto
        {
            Success = true,
            User = dto
        };
    }

    public async Task<UserResponseDto> UpdateUserAsync(Guid userId, UpdateUserDto dto, Guid editorId)
    {
        var user = await _context.Users
            .Include(u => u.Role)
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

        if (dto.Role != null && dto.Role != user.Role?.Code)
        {
            var newRole = await _context.Roles.FirstOrDefaultAsync(r => r.Code == dto.Role);
            if (newRole == null)
            {
                return new UserResponseDto
                {
                    Success = false,
                    Message = "Role không hợp lệ"
                };
            }

            auditLogs.Add(CreateAuditLog(userId, editorId, "UPDATE", "Role", user.Role?.Code, dto.Role));
            user.RoleId = newRole.Id;
            user.Role = newRole;
        }

        user.UpdatedAt = DateTime.UtcNow;

        if (auditLogs.Any())
        {
            await _context.UserAuditLogs.AddRangeAsync(auditLogs);
        }

        await _context.SaveChangesAsync();

        var outDto = MapToAdminUserDto(user);
        await ApplySupabaseAuthToDtoAsync(outDto, userId);

        return new UserResponseDto
        {
            Success = true,
            Message = "Cập nhật user thành công",
            User = outDto
        };
    }

    public async Task<UserResponseDto> SuspendUserAsync(Guid userId, SuspendUserDto dto, Guid adminId)
    {
        return await UpdateUserAccountStatusAsync(userId,
            new UpdateUserAccountStatusDto { Status = (short)UserStatus.Suspended, Reason = dto.Reason },
            adminId);
    }

    public async Task<UserResponseDto> UnsuspendUserAsync(Guid userId, Guid adminId)
    {
        return await UpdateUserAccountStatusAsync(userId,
            new UpdateUserAccountStatusDto { Status = (short)UserStatus.Active },
            adminId);
    }

    public async Task<UserResponseDto> UpdateUserAccountStatusAsync(
        Guid userId,
        UpdateUserAccountStatusDto dto,
        Guid adminId)
    {
        if (dto.Status is not ((short)UserStatus.Inactive) and not ((short)UserStatus.Active)
            and not ((short)UserStatus.Suspended))
        {
            return new UserResponseDto { Success = false, Message = "Trạng thái không hợp lệ" };
        }

        if (dto.Status == (short)UserStatus.Suspended && string.IsNullOrWhiteSpace(dto.Reason))
        {
            return new UserResponseDto { Success = false, Message = "Vui lòng nhập lý do khi khóa tài khoản" };
        }

        if (userId == adminId && dto.Status != (short)UserStatus.Active)
        {
            return new UserResponseDto { Success = false, Message = "Không thể thay đổi trạng thái chính tài khoản của mình" };
        }

        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Orders)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return new UserResponseDto { Success = false, Message = "Không tìm thấy user" };
        }

        if (user.Role?.Code == "admin" && dto.Status != (short)UserStatus.Active)
        {
            return new UserResponseDto { Success = false, Message = "Không thể khóa hoặc vô hiệu hóa tài khoản admin" };
        }

        if (dto.Status == (short)UserStatus.Active)
        {
            if (user.Status == (short)UserStatus.Active)
            {
                return new UserResponseDto { Success = false, Message = "Tài khoản đang hoạt động" };
            }

            var oldStatus = user.Status;
            var oldReason = user.SuspensionReason;
            user.Status = (short)UserStatus.Active;
            user.SuspensionReason = null;
            user.SuspendedAt = null;
            user.SuspendedBy = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.UserAuditLogs.AddAsync(CreateAuditLog(userId, adminId, "ACTIVATE", "Status",
                $"{oldStatus}:{oldReason}", "1"));
            await _context.SaveChangesAsync();

            var dtoOut = MapToAdminUserDto(user);
            await ApplySupabaseAuthToDtoAsync(dtoOut, userId);
            return new UserResponseDto { Success = true, Message = "Đã kích hoạt tài khoản", User = dtoOut };
        }

        if (IsAccountLocked(user.Status, user.SuspensionReason) && dto.Status == (short)UserStatus.Suspended)
        {
            return new UserResponseDto { Success = false, Message = "Tài khoản đã bị khóa" };
        }

        var prev = user.Status;
        var prevReason = user.SuspensionReason;
        user.Status = dto.Status;
        user.UpdatedAt = DateTime.UtcNow;

        if (dto.Status == (short)UserStatus.Suspended)
        {
            user.SuspensionReason = dto.Reason!.Trim();
            user.SuspendedAt = DateTime.UtcNow;
            user.SuspendedBy = adminId;
            await _context.UserAuditLogs.AddAsync(CreateAuditLog(userId, adminId, "SUSPEND", "Status",
                prev.ToString(), $"2:{dto.Reason}"));
        }
        else if (dto.Status == (short)UserStatus.Inactive)
        {
            user.SuspensionReason = null;
            user.SuspendedAt = null;
            user.SuspendedBy = null;
            await _context.UserAuditLogs.AddAsync(CreateAuditLog(userId, adminId, "DEACTIVATE", "Status",
                prev.ToString(), "0"));
        }

        await _context.SaveChangesAsync();

        var mapped = MapToAdminUserDto(user);
        await ApplySupabaseAuthToDtoAsync(mapped, userId);

        return new UserResponseDto
        {
            Success = true,
            Message = "Đã cập nhật trạng thái tài khoản",
            User = mapped
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

    public async Task<UserAddressesResponseDto> GetUserAddressesAsync(Guid userId)
    {
        var exists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!exists)
            return new UserAddressesResponseDto { Success = false, Message = "Không tìm thấy user" };

        var list = await _context.Addresses
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new AdminUserAddressDto
            {
                Id = a.Id,
                Label = a.Label,
                FullName = a.FullName,
                Phone = a.Phone,
                AddressLine1 = a.AddressLine1,
                AddressLine2 = a.AddressLine2,
                Ward = a.Ward,
                District = a.District,
                City = a.City,
                Province = a.Province,
                PostalCode = a.PostalCode,
                Country = a.Country,
                IsDefault = a.IsDefault,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        return new UserAddressesResponseDto { Success = true, Addresses = list };
    }

    public async Task<UserWalletDetailResponseDto> GetUserWalletDetailsAsync(Guid userId)
    {
        var exists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!exists)
            return new UserWalletDetailResponseDto { Success = false, Message = "Không tìm thấy user" };

        var response = new UserWalletDetailResponseDto { Success = true };

        var cw = await _context.CustomerWallets
            .AsNoTracking()
            .Include(w => w.CustomerWalletLedgers)
            .FirstOrDefaultAsync(w => w.CustomerId == userId);

        if (cw != null)
        {
            response.Customer = new AdminCustomerWalletDetailDto
            {
                WalletId = cw.Id,
                AvailableBalance = cw.AvailableBalance,
                Currency = cw.Currency,
                Ledger = cw.CustomerWalletLedgers
                    .OrderByDescending(x => x.CreatedAt)
                    .Take(100)
                    .Select(x => new AdminWalletLedgerEntryDto
                    {
                        Id = x.Id,
                        Type = x.Type,
                        Amount = x.Amount,
                        Currency = x.Currency,
                        ReferenceType = x.ReferenceType,
                        ReferenceId = x.ReferenceId,
                        Note = x.Note,
                        CreatedAt = x.CreatedAt
                    })
                    .ToList()
            };
        }

        var sw = await _context.SellerWallets
            .AsNoTracking()
            .Include(w => w.SellerWalletLedgers)
            .FirstOrDefaultAsync(w => w.SellerId == userId);

        if (sw != null)
        {
            response.Seller = new AdminSellerWalletDetailDto
            {
                WalletId = sw.Id,
                AvailableBalance = sw.AvailableBalance,
                HeldBalance = sw.HeldBalance,
                PendingBalance = sw.PendingBalance,
                Currency = sw.Currency,
                Ledger = sw.SellerWalletLedgers
                    .OrderByDescending(x => x.CreatedAt)
                    .Take(100)
                    .Select(x => new AdminWalletLedgerEntryDto
                    {
                        Id = x.Id,
                        Type = x.Type,
                        Amount = x.Amount,
                        Currency = x.Currency,
                        ReferenceType = x.ReferenceType,
                        ReferenceId = x.ReferenceId,
                        Note = x.Note,
                        CreatedAt = x.CreatedAt
                    })
                    .ToList()
            };
        }

        return response;
    }

    public async Task<UserProductReviewsResponseDto> GetUserProductReviewsAsync(Guid userId, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var exists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!exists)
            return new UserProductReviewsResponseDto { Success = false, Message = "Không tìm thấy user" };

        var q = _context.ProductReviews.AsNoTracking().Where(r => r.UserId == userId);
        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new AdminUserProductReviewDto
            {
                Id = r.Id,
                ProductId = r.ProductId,
                ProductName = r.Product.Name,
                Rating = r.Rating,
                Title = r.Title,
                Content = r.Content,
                Status = r.Status,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        return new UserProductReviewsResponseDto
        {
            Success = true,
            Reviews = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<UserShopReviewsResponseDto> GetUserShopReviewsAsync(Guid userId, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var exists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!exists)
            return new UserShopReviewsResponseDto { Success = false, Message = "Không tìm thấy user" };

        var q = _context.ProductReviews
            .AsNoTracking()
            .Include(r => r.Product)
            .ThenInclude(p => p.Shop)
            .Where(r => r.UserId == userId);
            
        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new AdminUserShopReviewDto
            {
                Id = r.Id,
                ShopId = r.Product.ShopId,
                ShopName = r.Product.Shop.Name,
                Rating = r.Rating,
                Title = r.Product.Name,
                Content = r.Content,
                Status = r.Status,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        return new UserShopReviewsResponseDto
        {
            Success = true,
            Reviews = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<SimpleMessageResponseDto> SendPasswordResetEmailAsync(Guid userId)
    {
        var email = await _authResolver.GetEmailByUserIdAsync(userId);
        if (string.IsNullOrWhiteSpace(email))
        {
            return new SimpleMessageResponseDto { Success = false, Message = "Không tìm thấy email đăng nhập (Supabase)" };
        }

        var supabaseUrl = _configuration["Supabase:Url"]?.TrimEnd('/');
        var anonKey = _configuration["Supabase:AnonKey"];
        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(anonKey))
        {
            return new SimpleMessageResponseDto { Success = false, Message = "Thiếu cấu hình Supabase" };
        }

        try
        {
            var http = _httpClientFactory.CreateClient();
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{supabaseUrl}/auth/v1/recover");
            req.Headers.TryAddWithoutValidation("apikey", anonKey);
            req.Content = JsonContent.Create(new { email });
            var res = await http.SendAsync(req);
            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync();
                _logger.LogWarning("Supabase recover failed: {Status} {Body}", res.StatusCode, err);
                return new SimpleMessageResponseDto
                {
                    Success = false,
                    Message = "Không gửi được email đặt lại mật khẩu"
                };
            }

            return new SimpleMessageResponseDto
            {
                Success = true,
                Message = "Đã gửi email đặt lại mật khẩu (nếu tài khoản tồn tại)"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendPasswordResetEmailAsync");
            return new SimpleMessageResponseDto { Success = false, Message = "Lỗi khi gọi Supabase" };
        }
    }

    private async Task ApplySupabaseAuthToDtoAsync(AdminUserDto dto, Guid userId)
    {
        var enrich = await _authResolver.GetSupabaseAuthEnrichmentAsync(userId);
        if (enrich == null)
            return;
        dto.Email = enrich.Email;
        dto.AvatarUrl = enrich.AvatarUrl;
        dto.Supabase = enrich.Details;
    }

    private static bool IsAccountLocked(short status, string? suspensionReason)
    {
        if (status == (short)UserStatus.Suspended)
            return true;
        // Legacy: khóa lưu Status=Inactive nhưng có lý do
        return status == (short)UserStatus.Inactive && !string.IsNullOrWhiteSpace(suspensionReason);
    }

    private AdminUserDto MapToAdminUserDto(User user)
    {
        return new AdminUserDto
        {
            Id = user.Id,
            UserCode = user.UserCode,
            FullName = user.FullName,
            Phone = user.Phone,
            Role = user.Role?.Code ?? string.Empty,
            Status = user.Status,
            StatusName = GetStatusDisplayName(user.Status, user.SuspensionReason),
            HasOrders = user.Orders?.Any() ?? false,
            SuspensionReason = user.SuspensionReason,
            SuspendedAt = user.SuspendedAt,
            SuspendedBy = user.SuspendedBy,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    private static string GetStatusDisplayName(short status, string? suspensionReason)
    {
        if (status == (short)UserStatus.Inactive && !string.IsNullOrWhiteSpace(suspensionReason))
            return "Suspended";

        return status switch
        {
            (short)UserStatus.Inactive => "Inactive",
            (short)UserStatus.Active => "Active",
            (short)UserStatus.Suspended => "Suspended",
            (short)UserStatus.Deleted => "Deleted",
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
