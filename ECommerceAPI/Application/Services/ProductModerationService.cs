using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class ProductModerationService : IProductModerationService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notifications;
    private readonly ILogger<ProductModerationService> _logger;
    private readonly ICartService _cartService;

    public ProductModerationService(
        ApplicationDbContext context,
        INotificationService notifications,
        ILogger<ProductModerationService> logger,
        ICartService cartService)
    {
        _context = context;
        _notifications = notifications;
        _logger = logger;
        _cartService = cartService;
    }

    public async Task<ProductModerationListResponseDto> GetAllProductsAsync(
        int page, 
        int pageSize, 
        short? status = null,
        Guid? shopId = null,
        string? search = null)
    {
        try
        {
            var query = _context.Products
                .Include(p => p.Shop)
                .Include(p => p.Category)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(p => p.Status == status.Value);
            }

            if (shopId.HasValue)
            {
                query = query.Where(p => p.ShopId == shopId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(searchLower));
            }

            var totalCount = await query.CountAsync();

            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductModerationDto
                {
                    Id = p.Id,
                    ProductCode = p.ProductCode,
                    Name = p.Name,
                    ShopId = p.ShopId,
                    ShopName = p.Shop.Name,
                    Status = p.Status,
                    StatusName = ((ProductStatus)p.Status).ToString(),
                    BasePrice = p.BasePrice,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    Description = null,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    ImageUrls = p.ProductImages
                        .OrderBy(img => img.SortOrder)
                        .Select(img => img.ImageUrl)
                        .ToList(),
                    LastApprovedSnapshotJson = null,
                    TagNames = new List<string>(),
                    MaterialNames = new List<string>(),
                    BaseInventoryQuantity = null,
                    Variants = new List<ProductApprovedVariantSnapshotDto>(),
                })
                .ToListAsync();

            return new ProductModerationListResponseDto
            {
                Success = true,
                Products = products,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting products for moderation");
            return new ProductModerationListResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi lấy danh sách sản phẩm"
            };
        }
    }

    public async Task<ProductModerationResponseDto> GetProductByIdAsync(Guid productId)
    {
        try
        {
            var p = await _context.Products
                .AsNoTracking()
                .Include(x => x.Shop)
                .Include(x => x.Category)
                .Include(x => x.ProductImages)
                .Include(x => x.ProductTags).ThenInclude(pt => pt.Tag)
                .Include(x => x.ProductMaterials).ThenInclude(pm => pm.Material)
                .Include(x => x.ProductVariants)
                .Include(x => x.Inventories)
                .FirstOrDefaultAsync(x => x.Id == productId);

            // Load local meta với Include — Split() không thể dịch sang SQL nên map sau khi fetch
            var rawMeta = await _context.ProductLocalMetas
                .AsNoTracking()
                .Include(m => m.LocalSpecialtyProfile)
                .Where(m => m.ProductId == productId)
                .FirstOrDefaultAsync();

            var localMeta = rawMeta == null ? null : new ProductLocalMetaModerationDto
            {
                ProfileId       = rawMeta.LocalSpecialtyProfileId,
                ProvinceName    = rawMeta.LocalSpecialtyProfile.ProvinceName,
                ArchetypeName   = rawMeta.LocalSpecialtyProfile.ArchetypeName,
                DisplayNote     = rawMeta.LocalSpecialtyProfile.DisplayNote,
                MismatchWarning = rawMeta.MismatchWarning,
                SelectedTraits  = string.IsNullOrEmpty(rawMeta.SelectedTraitsPipe)
                    ? new List<string>()
                    : rawMeta.SelectedTraitsPipe.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList(),
                ExpectedTraits  = string.IsNullOrEmpty(rawMeta.LocalSpecialtyProfile.ExpectedTraitsPipe)
                    ? new List<string>()
                    : rawMeta.LocalSpecialtyProfile.ExpectedTraitsPipe.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList(),
            };

            if (p == null)
            {
                return new ProductModerationResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy sản phẩm"
                };
            }

            var snap = ProductApprovedSnapshotBuilder.ToDto(p);
            var product = new ProductModerationDto
            {
                Id = p.Id,
                ProductCode = p.ProductCode,
                Name = p.Name,
                ShopId = p.ShopId,
                ShopName = p.Shop.Name,
                Status = p.Status,
                StatusName = ((ProductStatus)p.Status).ToString(),
                BasePrice = p.BasePrice,
                CategoryId = p.CategoryId,
                CategoryName = p.Category?.Name,
                Description = p.Description,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                ImageUrls = snap.ImageUrls,
                LastApprovedSnapshotJson = p.LastApprovedSnapshotJson,
                TagNames = snap.TagNames,
                MaterialNames = snap.MaterialNames,
                BaseInventoryQuantity = snap.BaseInventoryQuantity,
                Variants = snap.Variants,
                LocalMeta = localMeta,
            };

            return new ProductModerationResponseDto
            {
                Success = true,
                Product = product
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product: {ProductId}", productId);
            return new ProductModerationResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi lấy thông tin sản phẩm"
            };
        }
    }

    public async Task<ProductModerationResponseDto> HideProductAsync(
        Guid productId, 
        HideProductDto dto, 
        Guid adminId)
    {
        try
        {
            var product = await _context.Products.FindAsync(productId);

            if (product == null)
            {
                return new ProductModerationResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy sản phẩm"
                };
            }

            var wasPending = product.Status == (short)ProductStatus.PendingApproval;
            var wasActive = product.Status == (short)ProductStatus.Active;
            var previousStatus = product.Status;

            product.Status = (short)ProductStatus.Hidden;
            product.UpdatedAt = DateTime.UtcNow;

            if (previousStatus != (short)ProductStatus.Hidden)
                await _cartService.RemoveAllCartItemsForProductAsync(productId);

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Product hidden: {ProductId} by admin: {AdminId}. Reason: {Reason}", 
                productId, adminId, dto.Reason);

            if (wasPending)
            {
                var reason = string.IsNullOrWhiteSpace(dto.Reason) ? "Không có lý do cụ thể." : dto.Reason.Trim();
                await TryNotifyShopOwnerProductAsync(
                    product.ShopId,
                    product.Id,
                    "Sản phẩm chưa được duyệt (ẩn)",
                    $"Sản phẩm \"{product.Name}\" (mã: {product.ProductCode}) chưa được chấp nhận lên sàn. Lý do: {reason}");
            }
            else if (wasActive)
            {
                var reason = string.IsNullOrWhiteSpace(dto.Reason) ? "—" : dto.Reason.Trim();
                await TryNotifyShopOwnerProductAsync(
                    product.ShopId,
                    product.Id,
                    "Sản phẩm đã bị ẩn",
                    $"Sản phẩm \"{product.Name}\" (mã: {product.ProductCode}) đã bị quản trị viên ẩn khỏi sàn. Lý do: {reason}");
            }

            return new ProductModerationResponseDto
            {
                Success = true,
                Message = "Ẩn sản phẩm thành công"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hiding product: {ProductId}", productId);
            return new ProductModerationResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi ẩn sản phẩm"
            };
        }
    }

    public async Task<ProductModerationResponseDto> UnhideProductAsync(Guid productId, Guid adminId)
    {
        try
        {
            var product = await _context.Products.FindAsync(productId);

            if (product == null)
            {
                return new ProductModerationResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy sản phẩm"
                };
            }

            product.Status = (short)ProductStatus.Active;
            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Product unhidden: {ProductId} by admin: {AdminId}", productId, adminId);

            await ProductApprovedSnapshotBuilder.UpdateLastApprovedJsonAsync(_context, productId);

            return new ProductModerationResponseDto
            {
                Success = true,
                Message = "Hiển thị lại sản phẩm thành công"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unhiding product: {ProductId}", productId);
            return new ProductModerationResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi hiển thị lại sản phẩm"
            };
        }
    }

    public async Task<ProductModerationResponseDto> ApproveProductAsync(Guid productId, Guid adminId)
    {
        try
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return new ProductModerationResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy sản phẩm"
                };
            }

            if (product.Status != (short)ProductStatus.PendingApproval)
            {
                return new ProductModerationResponseDto
                {
                    Success = false,
                    Message = "Sản phẩm không ở trạng thái chờ duyệt."
                };
            }

            product.Status = (short)ProductStatus.Active;
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Product approved: {ProductId} by admin: {AdminId}", productId, adminId);

            await ProductApprovedSnapshotBuilder.UpdateLastApprovedJsonAsync(_context, productId);

            await TryNotifyShopOwnerProductAsync(
                product.ShopId,
                product.Id,
                "Sản phẩm đã được duyệt",
                $"Sản phẩm \"{product.Name}\" (mã: {product.ProductCode}) đã được quản trị viên phê duyệt. Sản phẩm đang hiển thị trên sàn.");

            return new ProductModerationResponseDto
            {
                Success = true,
                Message = "Đã phê duyệt sản phẩm."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving product: {ProductId}", productId);
            return new ProductModerationResponseDto
            {
                Success = false,
                Message = "Có lỗi khi phê duyệt sản phẩm"
            };
        }
    }

    public async Task<ProductModerationResponseDto> RejectProductAsync(
        Guid productId,
        RejectProductDto dto,
        Guid adminId)
    {
        try
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return new ProductModerationResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy sản phẩm"
                };
            }

            if (product.Status != (short)ProductStatus.PendingApproval)
            {
                return new ProductModerationResponseDto
                {
                    Success = false,
                    Message = "Sản phẩm không ở trạng thái chờ duyệt."
                };
            }

            product.Status = (short)ProductStatus.Draft;
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Product rejected to draft: {ProductId} by admin: {AdminId}", productId, adminId);

            var reason = string.IsNullOrWhiteSpace(dto?.Reason) ? "—" : dto.Reason!.Trim();
            await TryNotifyShopOwnerProductAsync(
                product.ShopId,
                product.Id,
                "Cập nhật sản phẩm chưa được duyệt",
                $"Sản phẩm \"{product.Name}\" (mã: {product.ProductCode}) cần chỉnh sửa và gửi lại. Ghi chú: {reason}");

            var get = await GetProductByIdAsync(productId);
            if (get is { Success: true, Product: not null })
            {
                get.Message = "Đã từ chối. Sản phẩm về nháp để shop chỉnh sửa.";
                return get;
            }

            return new ProductModerationResponseDto
            {
                Success = true,
                Message = "Đã từ chối. Sản phẩm về nháp."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting product: {ProductId}", productId);
            return new ProductModerationResponseDto
            {
                Success = false,
                Message = "Có lỗi khi từ chối sản phẩm"
            };
        }
    }

    public async Task<ProductModerationResponseDto> RemoveProductAsync(
        Guid productId, 
        RemoveProductDto dto, 
        Guid adminId)
    {
        try
        {
            var product = await _context.Products.FindAsync(productId);

            if (product == null)
            {
                return new ProductModerationResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy sản phẩm"
                };
            }

            var wasPending = product.Status == (short)ProductStatus.PendingApproval;

            product.Status = (short)ProductStatus.Removed;
            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Product removed: {ProductId} by admin: {AdminId}. Reason: {Reason}", 
                productId, adminId, dto.Reason);

            var reason = string.IsNullOrWhiteSpace(dto.Reason) ? "—" : dto.Reason.Trim();
            if (wasPending)
            {
                await TryNotifyShopOwnerProductAsync(
                    product.ShopId,
                    product.Id,
                    "Sản phẩm bị từ chối (gỡ)",
                    $"Sản phẩm \"{product.Name}\" (mã: {product.ProductCode}) ở trạng thái chờ duyệt đã bị gỡ. Lý do: {reason}");
            }
            else
            {
                await TryNotifyShopOwnerProductAsync(
                    product.ShopId,
                    product.Id,
                    "Sản phẩm bị gỡ khỏi nền tảng",
                    $"Sản phẩm \"{product.Name}\" (mã: {product.ProductCode}) đã bị gỡ vĩnh viễn. Lý do: {reason}");
            }

            return new ProductModerationResponseDto
            {
                Success = true,
                Message = "Gỡ sản phẩm thành công"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing product: {ProductId}", productId);
            return new ProductModerationResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi gỡ sản phẩm"
            };
        }
    }

    private async Task TryNotifyShopOwnerProductAsync(
        Guid shopId,
        Guid productId,
        string title,
        string content)
    {
        try
        {
            var ownerId = await _context.Shops
                .AsNoTracking()
                .Where(s => s.Id == shopId)
                .Select(s => s.OwnerId)
                .FirstOrDefaultAsync();

            if (ownerId == Guid.Empty)
                return;

            await _notifications.PublishAsync(
                ownerId,
                nameof(NotificationType.Shop),
                title,
                content,
                "Product",
                productId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Gửi thông báo shop {ShopId} / sản phẩm {ProductId} thất bại (đã bỏ qua).",
                shopId, productId);
        }
    }
}
