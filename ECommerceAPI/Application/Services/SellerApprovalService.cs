using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ECommerceAPI.Application.Services;

public class SellerApprovalService : ISellerApprovalService
{
    private readonly ApplicationDbContext _context;
    private readonly IUserAuthEmailResolver _authEmailResolver;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SellerApprovalService> _logger;
    private readonly INotificationService _notificationService;

    public SellerApprovalService(
        ApplicationDbContext context,
        IUserAuthEmailResolver authEmailResolver,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SellerApprovalService> logger,
        INotificationService notificationService)
    {
        _context = context;
        _authEmailResolver = authEmailResolver;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _notificationService = notificationService;
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

        var ownerEmails = await ResolveOwnerEmailsByUserIdsAsync(shops.Select(s => s.OwnerId));

        return new ShopListResponseDto
        {
            Success = true,
            Shops = shops.Select(s => MapToDto(s, ownerEmails.GetValueOrDefault(s.OwnerId))).ToList(),
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

        var ownerEmail = await _authEmailResolver.GetEmailByUserIdAsync(shop.OwnerId);

        return new ShopResponseDto
        {
            Success = true,
            Shop = MapToDto(shop, ownerEmail)
        };
    }

    public async Task<ShopResponseDto> ApproveShopAsync(Guid shopId, ApproveSellerDto dto, Guid adminId)
    {
        var shop = await _context.Shops
            .Include(s => s.Owner).ThenInclude(o => o!.Role)
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

        if (string.IsNullOrWhiteSpace(shop.Phone)
            || string.IsNullOrWhiteSpace(shop.AddressLine)
            || string.IsNullOrWhiteSpace(shop.WardCode)
            || !shop.DistrictId.HasValue
            || !shop.ProvinceId.HasValue
            || string.IsNullOrWhiteSpace(shop.City))
        {
            return new ShopResponseDto
            {
                Success = false,
                Message = "Shop thiếu thông tin liên hệ/địa chỉ. Vui lòng cập nhật trước khi phê duyệt."
            };
        }

        if (!shop.GhnShopId.HasValue || shop.GhnShopId.Value <= 0)
        {
            var createGhnResult = await CreateGhnShopAsync(shop);
            if (!createGhnResult.Success)
            {
                return new ShopResponseDto
                {
                    Success = false,
                    Message = createGhnResult.ErrorMessage
                };
            }

            shop.GhnShopId = createGhnResult.ShopId;
        }

        shop.VerificationStatus = 1;
        shop.Status = 1;
        shop.VerifiedAt = DateTime.UtcNow;
        shop.VerifiedBy = adminId;
        shop.RejectionReason = null;
        shop.UpdatedAt = DateTime.UtcNow;

        if (shop.Owner != null && shop.Owner.Role?.Code == "customer")
        {
            var sellerRole = await _context.Roles.FirstOrDefaultAsync(r => r.Code == "seller");
            if (sellerRole != null)
            {
                shop.Owner.RoleId = sellerRole.Id;
                shop.Owner.Role = sellerRole;
            }
            shop.Owner.UpdatedAt = DateTime.UtcNow;
            
            // Tạo ví cho seller mới (nếu chưa có)
            var existingWallet = await _context.SellerWallets
                .FirstOrDefaultAsync(w => w.SellerId == shop.OwnerId);
                
            if (existingWallet == null)
            {
                var wallet = new SellerWallet
                {
                    Id = Guid.NewGuid(),
                    SellerId = shop.OwnerId,
                    AvailableBalance = 0,
                    HeldBalance = 0,
                    PendingBalance = 0,
                    Currency = "VND",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.SellerWallets.AddAsync(wallet);
            }
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

        await SaveChangesWithAdminContextAsync(adminId);

        // Gửi in-app notification + email cho seller
        try
        {
            var emailHtml = $"""
                <html><body style="font-family:sans-serif;color:#333">
                  <h2 style="color:#e87f19">🎉 Chúc mừng! Shop của bạn đã được duyệt</h2>
                  <p>Shop <strong>{shop.Name}</strong> đã được phê duyệt thành công.</p>
                  <p>Bạn có thể bắt đầu đăng sản phẩm và bán hàng ngay bây giờ.</p>
                  <p style="color:#e87f19;font-weight:bold">⚠️ Lưu ý: Vui lòng đăng xuất và đăng nhập lại để hệ thống cập nhật quyền Seller.</p>
                  {(string.IsNullOrWhiteSpace(dto.Note) ? "" : $"<p>Ghi chú từ admin: {dto.Note}</p>")}
                </body></html>
                """;

            await _notificationService.PublishAsync(
                userId: shop.OwnerId,
                type: "Shop",
                title: "Shop của bạn đã được duyệt! 🎉",
                content: $"Shop \"{shop.Name}\" đã được phê duyệt. Đăng xuất và đăng nhập lại để sử dụng tính năng Seller.",
                referenceType: "shop",
                referenceId: shop.Id,
                queueEmail: true,
                emailHtmlBody: emailHtml,
                emailSubjectOverride: $"[EcomViet] Shop {shop.Name} đã được duyệt");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể gửi notification duyệt shop cho seller {SellerId}", shop.OwnerId);
        }

        var updatedShop = await _context.Shops
            .Include(s => s.Owner)
            .Include(s => s.ShopDocuments)
            .Include(s => s.VerifiedByNavigation)
            .FirstOrDefaultAsync(s => s.Id == shopId);

        var ownerEmail = updatedShop != null
            ? await _authEmailResolver.GetEmailByUserIdAsync(updatedShop.OwnerId)
            : null;

        return new ShopResponseDto
        {
            Success = true,
            Message = "Đã duyệt shop thành công. Seller có thể bắt đầu bán hàng.",
            Shop = MapToDto(updatedShop!, ownerEmail)
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

        await SaveChangesWithAdminContextAsync(adminId);

        // Gửi notification từ chối cho seller
        try
        {
            await _notificationService.PublishAsync(
                userId: shop.OwnerId,
                type: "Shop",
                title: "Yêu cầu mở shop bị từ chối",
                content: $"Shop \"{shop.Name}\" chưa được duyệt. Lý do: {dto.Reason}. Vui lòng điều chỉnh thông tin và gửi lại.",
                referenceType: "shop",
                referenceId: shop.Id,
                queueEmail: true,
                emailSubjectOverride: $"[EcomViet] Yêu cầu mở shop {shop.Name} bị từ chối");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể gửi notification từ chối shop cho seller {SellerId}", shop.OwnerId);
        }

        var updatedShop = await _context.Shops
            .Include(s => s.Owner)
            .Include(s => s.ShopDocuments)
            .Include(s => s.VerifiedByNavigation)
            .FirstOrDefaultAsync(s => s.Id == shopId);

        var ownerEmail = updatedShop != null
            ? await _authEmailResolver.GetEmailByUserIdAsync(updatedShop.OwnerId)
            : null;

        return new ShopResponseDto
        {
            Success = true,
            Message = "Đã từ chối shop. Seller có thể resubmit sau khi sửa thông tin.",
            Shop = MapToDto(updatedShop!, ownerEmail)
        };
    }

    private static ShopVerificationDto MapToDto(Shop s, string? ownerEmail = null)
    {
        return new ShopVerificationDto
        {
            Id = s.Id,
            ShopCode = s.ShopCode,
            OwnerId = s.OwnerId,
            OwnerName = s.Owner?.FullName,
            OwnerEmail = ownerEmail,
            Name = s.Name,
            Slug = s.Slug,
            Description = s.Description,
            LogoUrl = s.LogoUrl,
            Phone = s.Phone,
            AddressLine = s.AddressLine,
            WardCode = s.WardCode,
            DistrictId = s.DistrictId,
            ProvinceId = s.ProvinceId,
            City = s.City,
            GhnShopId = s.GhnShopId,
            BusinessType = s.BusinessType,
            BusinessLicenseNumber = s.BusinessLicenseNumber,
            TaxCode = s.TaxCode,
            BankName = s.BankName,
            BankAccountNumber = s.BankAccountNumber,
            BankAccountName = s.BankAccountName,
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

        var ownerEmail = updatedShop != null
            ? await _authEmailResolver.GetEmailByUserIdAsync(updatedShop.OwnerId)
            : null;

        return new ShopResponseDto
        {
            Success = true,
            Message = "Đã kích hoạt shop thành công",
            Shop = MapToDto(updatedShop!, ownerEmail)
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

        var ownerEmail = updatedShop != null
            ? await _authEmailResolver.GetEmailByUserIdAsync(updatedShop.OwnerId)
            : null;

        var message = activeOrdersCount > 0
            ? $"Đã tạm ngưng shop. Lưu ý: Shop có {activeOrdersCount} đơn hàng đang xử lý, cần tiếp tục hoàn thành."
            : "Đã tạm ngưng shop thành công. Sản phẩm sẽ bị ẩn khỏi tìm kiếm.";

        return new ShopResponseDto
        {
            Success = true,
            Message = message,
            Shop = MapToDto(updatedShop!, ownerEmail)
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

        var ownerEmail = updatedShop != null
            ? await _authEmailResolver.GetEmailByUserIdAsync(updatedShop.OwnerId)
            : null;

        return new ShopResponseDto
        {
            Success = true,
            Message = "Đã đóng shop vĩnh viễn",
            Shop = MapToDto(updatedShop!, ownerEmail)
        };
    }

    private async Task<Dictionary<Guid, string?>> ResolveOwnerEmailsByUserIdsAsync(IEnumerable<Guid> userIds)
    {
        var distinctIds = userIds.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return new Dictionary<Guid, string?>();
        }

        var tasks = distinctIds.Select(async id => new
        {
            Id = id,
            Email = await _authEmailResolver.GetEmailByUserIdAsync(id)
        });

        var resolved = await Task.WhenAll(tasks);
        return resolved.ToDictionary(x => x.Id, x => x.Email);
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

    private async Task SaveChangesWithAdminContextAsync(Guid adminId)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync();

            await _context.Database.ExecuteSqlRawAsync(
                "SET LOCAL session_replication_role = replica");

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        });
    }

    private async Task<(bool Success, int? ShopId, string ErrorMessage)> CreateGhnShopAsync(Shop shop)
    {
        var ghnToken = (_configuration["GHN:Token"])?.Trim();
        var ghnBaseUrl = (_configuration["GHN:BaseUrl"] ?? string.Empty).TrimEnd('/');

        if (string.IsNullOrWhiteSpace(ghnToken))
        {
            return (false, null, "Thiếu cấu hình GHN Token, không thể tạo cửa hàng trên GHN.");
        }

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{ghnBaseUrl}/shiip/public-api/v2/shop/register");

        request.Headers.TryAddWithoutValidation("Token", ghnToken);
        request.Content = JsonContent.Create(new
        {
            district_id = shop.DistrictId!.Value,
            ward_code = shop.WardCode,
            name = shop.Name,
            phone = shop.Phone,
            address = shop.AddressLine
        });

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call GHN create-store API for shop {ShopId}", shop.Id);
            return (false, null, $"Không thể kết nối GHN để tạo cửa hàng: {ex.Message}");
        }

        var responseText = await response.Content.ReadAsStringAsync();
        GhnCreateShopResponse? ghnResponse;
        try
        {
            ghnResponse = JsonSerializer.Deserialize<GhnCreateShopResponse>(
                responseText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            ghnResponse = null;
        }

        if (!response.IsSuccessStatusCode || ghnResponse?.Code != 200 || ghnResponse.Data?.ShopId is null || ghnResponse.Data.ShopId <= 0)
        {
            var message = ghnResponse?.CodeMessageValue
                ?? ghnResponse?.CodeMessage
                ?? ghnResponse?.Message;

            if (string.IsNullOrWhiteSpace(message))
            {
                message = $"GHN trả về HTTP {(int)response.StatusCode}";
            }

            _logger.LogWarning(
                "GHN create-store failed for shop {ShopId}. StatusCode={StatusCode}, Response={Response}",
                shop.Id,
                (int)response.StatusCode,
                responseText);

            return (false, null, $"Tạo cửa hàng GHN thất bại: {message}");
        }

        return (true, ghnResponse.Data.ShopId, string.Empty);
    }

    private sealed class GhnCreateShopResponse
    {
        public int Code { get; set; }
        public string? Message { get; set; }

        [JsonPropertyName("code_message")]
        public string? CodeMessage { get; set; }

        [JsonPropertyName("code_message_value")]
        public string? CodeMessageValue { get; set; }

        public GhnCreateShopData? Data { get; set; }
    }

    private sealed class GhnCreateShopData
    {
        [JsonPropertyName("shop_id")]
        public int ShopId { get; set; }
    }
}
