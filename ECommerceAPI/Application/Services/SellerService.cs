using ECommerceAPI.Application;
using ECommerceAPI.Application.DTOs.Seller;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Hubs;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ECommerceAPI.Application.Services;

public class SellerService : ISellerService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<OrderTrackingHub> _hubContext;
    private readonly INotificationService _notifications;
    private readonly IUserAuthEmailResolver _authResolver;
    private readonly ISellerWalletReversalService _walletReversal;
    private readonly ISellerWalletReleaseService _walletRelease;
    private readonly IOrderNotificationEmailComposer _orderEmailComposer;

    public SellerService(
        ApplicationDbContext context,
        IHubContext<OrderTrackingHub> hubContext,
        INotificationService notifications,
        IUserAuthEmailResolver authResolver,
        ISellerWalletReversalService walletReversal,
        ISellerWalletReleaseService walletRelease,
        IOrderNotificationEmailComposer orderEmailComposer)
    {
        _context = context;
        _hubContext = hubContext;
        _notifications = notifications;
        _authResolver = authResolver;
        _walletReversal = walletReversal;
        _walletRelease = walletRelease;
        _orderEmailComposer = orderEmailComposer;
    }

    public async Task<ServiceResponse<ShopDto>> GetMyShopAsync(Guid userId)
    {
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.OwnerId == userId);

        if (shop == null)
        {
            return new ServiceResponse<ShopDto>
            {
                Success = false,
                Message = "Bạn chưa có shop"
            };
        }

        return new ServiceResponse<ShopDto>
        {
            Success = true,
            Data = new ShopDto
            {
                Id = shop.Id,
                ShopCode = shop.ShopCode,
                Name = shop.Name,
                Slug = shop.Slug,
                Description = shop.Description,
                LogoUrl = shop.LogoUrl,
                Phone = shop.Phone,
                AddressLine = shop.AddressLine,
                WardCode = shop.WardCode,
                DistrictId = shop.DistrictId,
                ProvinceId = shop.ProvinceId,
                City = shop.City,
                GhnShopId = shop.GhnShopId,
                Status = shop.Status,
                VerificationStatus = shop.VerificationStatus,
                CreatedAt = shop.CreatedAt
            }
        };
    }

    public async Task<ServiceResponse> UpdateShopAsync(Guid userId, UpdateShopDto dto)
    {
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.OwnerId == userId);

        if (shop == null)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = "Không tìm thấy shop"
            };
        }

        if (!string.IsNullOrEmpty(dto.Name))
            shop.Name = dto.Name;

        if (dto.Description != null)
            shop.Description = dto.Description;

        if (dto.LogoUrl != null)
            shop.LogoUrl = dto.LogoUrl;

        if (dto.Phone != null)
            shop.Phone = dto.Phone;

        if (dto.AddressLine != null)
            shop.AddressLine = dto.AddressLine;

        if (dto.WardCode != null)
            shop.WardCode = dto.WardCode;

        if (dto.DistrictId.HasValue)
            shop.DistrictId = dto.DistrictId;

        if (dto.ProvinceId.HasValue)
            shop.ProvinceId = dto.ProvinceId;

        if (dto.City != null)
            shop.City = dto.City;

        if (dto.GhnShopId.HasValue)
            shop.GhnShopId = dto.GhnShopId;

        shop.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new ServiceResponse
        {
            Success = true,
            Message = "Cập nhật shop thành công"
        };
    }

    // ==================== WALLET & WITHDRAWAL MANAGEMENT ====================

    public async Task<ServiceResponse<WalletDto>> GetMyWalletAsync(Guid userId)
    {
        var wallet = await _context.SellerWallets
            .FirstOrDefaultAsync(w => w.SellerId == userId);

        if (wallet == null)
        {
            return new ServiceResponse<WalletDto>
            {
                Success = false,
                Message = "Không tìm thấy ví"
            };
        }

        // Calculate total earnings and withdrawn
        var ledgers = await _context.SellerWalletLedgers
            .Where(l => l.WalletId == wallet.Id)
            .ToListAsync();

        var totalEarnings = ledgers.Where(l => l.Amount > 0).Sum(l => l.Amount);
        var totalWithdrawn = ledgers
            .Where(l => l.Amount < 0 && l.ReferenceType == WalletLedgerReferenceTypes.Withdrawal)
            .Sum(l => Math.Abs(l.Amount));
        var totalRefunded = ledgers
            .Where(l => l.Amount < 0 && l.ReferenceType == WalletLedgerReferenceTypes.OrderRefund)
            .Sum(l => Math.Abs(l.Amount));

        return new ServiceResponse<WalletDto>
        {
            Success = true,
            Data = new WalletDto
            {
                Id = wallet.Id,
                AvailableBalance = wallet.AvailableBalance,
                HeldBalance = wallet.HeldBalance,
                PendingBalance = wallet.PendingBalance,
                TotalEarnings = totalEarnings,
                TotalWithdrawn = totalWithdrawn,
                TotalRefunded = totalRefunded,
                UpdatedAt = wallet.UpdatedAt
            }
        };
    }

    public async Task<ServiceResponse<List<WithdrawalRequestDto>>> GetMyWithdrawalRequestsAsync(Guid userId, int page, int pageSize)
    {
        var requests = await _context.SellerWithdrawalRequests
            .Where(r => r.SellerId == userId)
            .OrderByDescending(r => r.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new WithdrawalRequestDto
            {
                Id = r.Id,
                SellerId = r.SellerId,
                Amount = r.Amount,
                BankName = r.BankName,
                BankAccountNumber = r.BankAccountNumber,
                BankAccountName = r.BankAccountName,
                Status = r.Status,
                RejectionReason = r.RejectionReason,
                RequestedAt = r.RequestedAt,
                ReviewedAt = r.ReviewedAt,
                ReviewedBy = r.ReviewedBy,
                AdminNote = r.AdminNote
            })
            .ToListAsync();

        return new ServiceResponse<List<WithdrawalRequestDto>>
        {
            Success = true,
            Data = requests
        };
    }

    public async Task<ServiceResponse<WithdrawalRequestDto>> CreateWithdrawalRequestAsync(Guid userId, CreateWithdrawalRequestDto dto)
    {
        var wallet = await _context.SellerWallets
            .FirstOrDefaultAsync(w => w.SellerId == userId);

        if (wallet == null)
        {
            return new ServiceResponse<WithdrawalRequestDto>
            {
                Success = false,
                Message = "Không tìm thấy ví"
            };
        }

        if (wallet.AvailableBalance < dto.Amount)
        {
            return new ServiceResponse<WithdrawalRequestDto>
            {
                Success = false,
                Message = $"Số dư không đủ. Số dư khả dụng: {wallet.AvailableBalance:N0} VND"
            };
        }

        // Check if there's a pending request
        var hasPendingRequest = await _context.SellerWithdrawalRequests
            .AnyAsync(r => r.SellerId == userId && r.Status == 0); // 0 = Pending

        if (hasPendingRequest)
        {
            return new ServiceResponse<WithdrawalRequestDto>
            {
                Success = false,
                Message = "Bạn đang có yêu cầu rút tiền chờ xử lý"
            };
        }

        var request = new SellerWithdrawalRequest
        {
            Id = Guid.NewGuid(),
            SellerId = userId,
            WalletId = wallet.Id,
            Amount = dto.Amount,
            Currency = wallet.Currency,
            BankName = dto.BankName,
            BankAccountNumber = dto.BankAccountNumber,
            BankAccountName = dto.BankAccountName,
            Status = 0, // Pending
            RequestedAt = DateTime.UtcNow
        };

        _context.SellerWithdrawalRequests.Add(request);

        // Reserve the amount
        wallet.AvailableBalance -= dto.Amount;
        wallet.PendingBalance += dto.Amount;
        wallet.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new ServiceResponse<WithdrawalRequestDto>
        {
            Success = true,
            Message = "Tạo yêu cầu rút tiền thành công",
            Data = new WithdrawalRequestDto
            {
                Id = request.Id,
                SellerId = request.SellerId,
                Amount = request.Amount,
                BankName = request.BankName,
                BankAccountNumber = request.BankAccountNumber,
                BankAccountName = request.BankAccountName,
                Status = request.Status,
                RequestedAt = request.RequestedAt
            }
        };
    }

    // ==================== PRODUCT MANAGEMENT ====================

    public async Task<ServiceResponse<List<ProductDto>>> GetMyProductsAsync(Guid userId, int page, int pageSize, short? status, string? search = null)
    {
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.OwnerId == userId);

        if (shop == null)
        {
            return new ServiceResponse<List<ProductDto>>
            {
                Success = false,
                Message = "Bạn chưa có shop"
            };
        }

        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.ProductImages)
            .Include(p => p.ProductVariants)
            .Include(p => p.Inventories)
            .Where(p => p.ShopId == shop.Id);

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(q) ||
                                     (p.Category != null && p.Category.Name.ToLower().Contains(q)));
        }

        var totalCount = await query.CountAsync();

        var products = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                ProductCode = p.ProductCode,
                Slug = p.Slug,
                ShopId = p.ShopId,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : null,
                Name = p.Name,
                Description = p.Description,
                BasePrice = p.BasePrice,
                Currency = p.Currency,
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                Images = p.ProductImages.OrderBy(i => i.SortOrder).Select(i => new ProductImageDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    DisplayOrder = (short)i.SortOrder
                }).ToList(),
                Variants = p.ProductVariants.Select(v => new ProductVariantDetailDto
                {
                    Id = v.Id,
                    VariantName = v.VariantName,
                    Sku = v.Sku,
                    Price = v.Price,
                    IsActive = v.IsActive,
                    Stock = p.Inventories.FirstOrDefault(i => i.VariantId == v.Id) != null 
                        ? p.Inventories.First(i => i.VariantId == v.Id).Quantity 
                        : 0,
                    Attributes = v.Attributes
                }).ToList(),
                TotalStock = p.Inventories.Sum(i => i.Quantity)
            })
            .ToListAsync();

        return new ServiceResponse<List<ProductDto>>
        {
            Success = true,
            Data = products,
            TotalCount = totalCount
        };
    }

    public async Task<ServiceResponse<ProductDto>> GetProductByIdAsync(Guid userId, Guid productId)
    {
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.OwnerId == userId);

        if (shop == null)
        {
            return new ServiceResponse<ProductDto>
            {
                Success = false,
                Message = "Bạn chưa có shop"
            };
        }

        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.ProductImages)
            .Include(p => p.ProductVariants)
            .Include(p => p.Inventories)
            .FirstOrDefaultAsync(p => p.Id == productId && p.ShopId == shop.Id);

        if (product == null)
        {
            return new ServiceResponse<ProductDto>
            {
                Success = false,
                Message = "Không tìm thấy sản phẩm"
            };
        }

        return new ServiceResponse<ProductDto>
        {
            Success = true,
            Data = new ProductDto
            {
                Id = product.Id,
                ProductCode = product.ProductCode,
                Slug = product.Slug,
                ShopId = product.ShopId,
                CategoryId = product.CategoryId,
                CategoryName = product.Category?.Name,
                Name = product.Name,
                Description = product.Description,
                BasePrice = product.BasePrice,
                Currency = product.Currency,
                Status = product.Status,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt,
                Images = product.ProductImages.OrderBy(i => i.SortOrder).Select(i => new ProductImageDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    DisplayOrder = (short)i.SortOrder
                }).ToList(),
                Variants = product.ProductVariants.Select(v => new ProductVariantDetailDto
                {
                    Id = v.Id,
                    VariantName = v.VariantName,
                    Sku = v.Sku,
                    Price = v.Price,
                    IsActive = v.IsActive,
                    Stock = product.Inventories.FirstOrDefault(i => i.VariantId == v.Id)?.Quantity ?? 0,
                    Attributes = v.Attributes
                }).ToList(),
                TotalStock = product.Inventories.Sum(i => i.Quantity)
            }
        };
    }

    public async Task<ServiceResponse<ProductDto>> CreateProductAsync(Guid userId, CreateProductDto dto)
    {
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.OwnerId == userId);

        if (shop == null)
        {
            return new ServiceResponse<ProductDto>
            {
                Success = false,
                Message = "Bạn chưa có shop"
            };
        }

        if (shop.Status != 1 || shop.VerificationStatus != 1)
        {
            return new ServiceResponse<ProductDto>
            {
                Success = false,
                Message = "Shop của bạn chưa được kích hoạt hoặc chưa được xác minh"
            };
        }

        var productCode = await GenerateUniqueProductCodeAsync();
        var slug = await GenerateUniqueProductSlugAsync(dto.Name);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            ProductCode = productCode,
            Slug = slug,
            ShopId = shop.Id,
            CategoryId = dto.CategoryId,
            Name = dto.Name,
            Description = dto.Description,
            BasePrice = dto.BasePrice,
            Currency = dto.Currency,
            Status = 0, // Draft
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);

        // Add images
        if (dto.ImageUrls != null && dto.ImageUrls.Any())
        {
            short order = 0;
            foreach (var imageUrl in dto.ImageUrls)
            {
                var image = new ProductImage
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    ImageUrl = imageUrl,
                    SortOrder = order++,
                    CreatedAt = DateTime.UtcNow
                };
                _context.ProductImages.Add(image);
            }
        }

        // Add variants
        if (dto.Variants != null && dto.Variants.Any())
        {
            foreach (var variantDto in dto.Variants)
            {
                var variant = new ProductVariant
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    VariantName = variantDto.VariantName,
                    Sku = variantDto.Sku,
                    Price = variantDto.Price,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    Attributes = NormalizeVariantAttributesForJsonb(variantDto.Attributes)
                };
                _context.ProductVariants.Add(variant);

                // Add inventory for variant
                var inventory = new Inventory
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    VariantId = variant.Id,
                    Quantity = variantDto.Quantity,
                    ReservedQuantity = 0,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Inventories.Add(inventory);
            }
        }
        else
        {
            // No variants - create default inventory
            var inventory = new Inventory
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                VariantId = null,
                Quantity = dto.Quantity,
                ReservedQuantity = 0,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Inventories.Add(inventory);
        }

        // Add tags
        if (dto.TagIds != null && dto.TagIds.Any())
        {
            foreach (var tagId in dto.TagIds)
            {
                var productTag = new ProductTag
                {
                    ProductId = product.Id,
                    TagId = tagId
                };
                _context.Set<ProductTag>().Add(productTag);
            }
        }

        // Add materials
        if (dto.MaterialIds != null && dto.MaterialIds.Any())
        {
            foreach (var materialId in dto.MaterialIds)
            {
                _context.Set<ProductMaterial>().Add(new ProductMaterial
                {
                    ProductId = product.Id,
                    MaterialId = materialId
                });
            }
        }

        await _context.SaveChangesAsync();

        return await GetProductByIdAsync(userId, product.Id);
    }

    public async Task<ServiceResponse> UpdateProductAsync(Guid userId, Guid productId, UpdateProductDto dto)
    {
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.OwnerId == userId);

        if (shop == null)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = "Bạn chưa có shop"
            };
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.ShopId == shop.Id);

        if (product == null)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = "Không tìm thấy sản phẩm"
            };
        }

        if (dto.CategoryId.HasValue)
            product.CategoryId = dto.CategoryId;

        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            var nextName = dto.Name.Trim();
            if (!string.Equals(nextName, product.Name, StringComparison.Ordinal))
            {
                product.Name = nextName;
                product.Slug = await GenerateUniqueProductSlugAsync(nextName, product.Id);
            }
        }

        if (dto.Description != null)
            product.Description = dto.Description;

        if (dto.BasePrice.HasValue)
            product.BasePrice = dto.BasePrice.Value;

        if (dto.Status.HasValue)
            product.Status = dto.Status.Value;

        product.UpdatedAt = DateTime.UtcNow;

        if (dto.ImageUrls != null)
        {
            var existingImages = await _context.ProductImages.Where(i => i.ProductId == product.Id).ToListAsync();
            _context.ProductImages.RemoveRange(existingImages);

            short order = 1;
            foreach (var url in dto.ImageUrls)
            {
                _context.ProductImages.Add(new ProductImage
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    ImageUrl = url,
                    SortOrder = order++,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        if (dto.TagIds != null)
        {
            var existingTags = await _context.Set<ProductTag>().Where(t => t.ProductId == product.Id).ToListAsync();
            _context.Set<ProductTag>().RemoveRange(existingTags);
            foreach (var tagId in dto.TagIds)
            {
                _context.Set<ProductTag>().Add(new ProductTag { ProductId = product.Id, TagId = tagId });
            }
        }

        if (dto.MaterialIds != null)
        {
            var existingMaterials = await _context.Set<ProductMaterial>().Where(m => m.ProductId == product.Id).ToListAsync();
            _context.Set<ProductMaterial>().RemoveRange(existingMaterials);
            foreach (var materialId in dto.MaterialIds)
            {
                _context.Set<ProductMaterial>().Add(new ProductMaterial { ProductId = product.Id, MaterialId = materialId });
            }
        }

        await _context.SaveChangesAsync();

        return new ServiceResponse
        {
            Success = true,
            Message = "Cập nhật sản phẩm thành công"
        };
    }

    public async Task<ServiceResponse> DeleteProductAsync(Guid userId, Guid productId)
    {
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.OwnerId == userId);

        if (shop == null)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = "Bạn chưa có shop"
            };
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.ShopId == shop.Id);

        if (product == null)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = "Không tìm thấy sản phẩm"
            };
        }

    
        product.Status = (short)ProductStatus.Hidden;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new ServiceResponse
        {
            Success = true,
            Message = "Ẩn sản phẩm thành công"
        };
    }

    public async Task<ServiceResponse<ProductVariantDetailDto>> AddProductVariantAsync(Guid userId, Guid productId, ProductVariantDto dto)
    {
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.OwnerId == userId);

        if (shop == null)
        {
            return new ServiceResponse<ProductVariantDetailDto>
            {
                Success = false,
                Message = "Bạn chưa có shop"
            };
        }

        if (shop.Status != 1 || shop.VerificationStatus != 1)
        {
            return new ServiceResponse<ProductVariantDetailDto>
            {
                Success = false,
                Message = "Shop của bạn chưa được kích hoạt hoặc chưa được xác minh"
            };
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.ShopId == shop.Id);

        if (product == null)
        {
            return new ServiceResponse<ProductVariantDetailDto>
            {
                Success = false,
                Message = "Không tìm thấy sản phẩm"
            };
        }

        var hadNoVariants = !await _context.ProductVariants.AnyAsync(v => v.ProductId == productId);
        if (hadNoVariants)
        {
            var baseInventories = await _context.Inventories
                .Where(i => i.ProductId == productId && i.VariantId == null)
                .ToListAsync();
            _context.Inventories.RemoveRange(baseInventories);
        }

        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            VariantName = dto.VariantName.Trim(),
            Sku = string.IsNullOrWhiteSpace(dto.Sku) ? null : dto.Sku.Trim(),
            Price = dto.Price,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Attributes = NormalizeVariantAttributesForJsonb(dto.Attributes)
        };
        _context.ProductVariants.Add(variant);

        _context.Inventories.Add(new Inventory
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            VariantId = variant.Id,
            Quantity = dto.Quantity,
            ReservedQuantity = 0,
            UpdatedAt = DateTime.UtcNow
        });

        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new ServiceResponse<ProductVariantDetailDto>
        {
            Success = true,
            Message = "Đã thêm biến thể",
            Data = new ProductVariantDetailDto
            {
                Id = variant.Id,
                VariantName = variant.VariantName,
                Sku = variant.Sku,
                Price = variant.Price,
                IsActive = variant.IsActive,
                Stock = dto.Quantity,
                Attributes = variant.Attributes
            }
        };
    }

    public async Task<ServiceResponse> UpdateProductVariantAsync(Guid userId, Guid productId, Guid variantId, UpdateProductVariantDto dto)
    {
        var shop = await _context.Shops.FirstOrDefaultAsync(s => s.OwnerId == userId);
        if (shop == null)
            return new ServiceResponse { Success = false, Message = "Bạn chưa có shop" };

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.ShopId == shop.Id);
        if (product == null)
            return new ServiceResponse { Success = false, Message = "Không tìm thấy sản phẩm" };

        var variant = await _context.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == productId);
        if (variant == null)
            return new ServiceResponse { Success = false, Message = "Không tìm thấy biến thể" };

        if (!string.IsNullOrWhiteSpace(dto.VariantName))
            variant.VariantName = dto.VariantName.Trim();

        if (dto.Sku != null)
            variant.Sku = string.IsNullOrWhiteSpace(dto.Sku) ? null : dto.Sku.Trim();

        if (dto.Price.HasValue)
            variant.Price = dto.Price.Value <= 0 ? null : dto.Price.Value;

        if (dto.Attributes != null)
            variant.Attributes = NormalizeVariantAttributesForJsonb(dto.Attributes);

        if (dto.IsActive.HasValue)
            variant.IsActive = dto.IsActive.Value;

        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new ServiceResponse { Success = true, Message = "Đã cập nhật biến thể" };
    }

    public async Task<ServiceResponse> UpdateInventoryAsync(Guid userId, Guid productId, UpdateInventoryDto dto)
    {
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.OwnerId == userId);

        if (shop == null)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = "Bạn chưa có shop"
            };
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.ShopId == shop.Id);

        if (product == null)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = "Không tìm thấy sản phẩm"
            };
        }

        var inventory = await _context.Inventories
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.VariantId == dto.VariantId);

        if (inventory == null)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = "Không tìm thấy inventory"
            };
        }

        inventory.Quantity = dto.Quantity;
        inventory.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new ServiceResponse
        {
            Success = true,
            Message = "Cập nhật kho thành công"
        };
    }

    public async Task<ServiceResponse<List<OrderDto>>> GetMyOrdersAsync(Guid userId, int page, int pageSize, short? status, string? search = null)
    {
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.OwnerId == userId);

        if (shop == null)
        {
            return new ServiceResponse<List<OrderDto>>
            {
                Success = false,
                Message = "Bạn chưa có shop"
            };
        }

        var query = _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.ShippingAddress)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.ProductImages)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Variant)
            .Where(o => o.ShopId == shop.Id);

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.ToLower();
            query = query.Where(o =>
                (o.OrderCode != null && o.OrderCode.ToLower().Contains(q)) ||
                (o.Customer.FullName != null && o.Customer.FullName.ToLower().Contains(q)) ||
                (o.Customer.Phone != null && o.Customer.Phone.Contains(q)));
        }

        var totalCount = await query.CountAsync();

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderDto
            {
                Id = o.Id,
                OrderCode = o.OrderCode,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer.FullName,
                CustomerPhone = o.Customer.Phone,
                TotalAmount = o.Total,
                Status = o.Status,
                CancelReason = o.CancelReason,
                ShippingAddress = o.ShippingAddress != null 
                    ? $"{o.ShippingAddress.AddressLine1}, {o.ShippingAddress.Ward}, {o.ShippingAddress.District}, {o.ShippingAddress.City}"
                    : o.ShipAddress,
                ProviderShippingFee = o.ProviderShippingFee,
                ShippingProvider = o.ShippingProvider,
                ShippingServiceId = o.ShippingServiceId,
                TrackingCode = o.TrackingCode,
                EstimatedDeliveryDate = o.EstimatedDeliveryDate,
                ActualDeliveryDate = o.ActualDeliveryDate,
                CreatedAt = o.CreatedAt,
                ShopGhnShopId = shop.GhnShopId,
                ShopFromDistrictId = shop.DistrictId,
                ShopFromWardCode = shop.WardCode,
                Items = o.OrderItems.Select(oi => new OrderItemDto
                {
                    Id = oi.Id,
                    ProductId = oi.ProductId,
                    ProductName = oi.Product.Name,
                    ProductThumbnailUrl = oi.Product.ProductImages
                        .OrderBy(pi => pi.SortOrder)
                        .Select(pi => pi.ImageUrl)
                        .FirstOrDefault(),
                    VariantName = oi.Variant != null ? oi.Variant.VariantName : null,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    TotalPrice = oi.LineTotal
                }).ToList()
            })
            .ToListAsync();

        var customerIds = orders.Select(o => o.CustomerId).Distinct().ToList();
        if (customerIds.Count > 0)
        {
            var avatarLookups = await Task.WhenAll(
                customerIds.Select(async customerId => new
                {
                    CustomerId = customerId,
                    Email = await _authResolver.GetEmailByUserIdAsync(customerId),
                    AvatarUrl = await _authResolver.GetAvatarUrlByUserIdAsync(customerId)
                }));

            var avatarByCustomer = avatarLookups.ToDictionary(x => x.CustomerId, x => x.AvatarUrl);
            var emailByCustomer = avatarLookups.ToDictionary(x => x.CustomerId, x => x.Email);
            foreach (var orderDto in orders)
            {
                orderDto.CustomerEmail = emailByCustomer.GetValueOrDefault(orderDto.CustomerId);
                orderDto.CustomerAvatarUrl = avatarByCustomer.GetValueOrDefault(orderDto.CustomerId);
            }
        }

        return new ServiceResponse<List<OrderDto>>
        {
            Success = true,
            Data = orders,
            TotalCount = totalCount
        };
    }

    public async Task<ServiceResponse<OrderDto>> GetOrderByIdAsync(Guid userId, Guid orderId)
    {
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.OwnerId == userId);

        if (shop == null)
        {
            return new ServiceResponse<OrderDto>
            {
                Success = false,
                Message = "Bạn chưa có shop"
            };
        }

        var order = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.ShippingAddress)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.ProductImages)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Variant)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.ShopId == shop.Id);

        if (order == null)
        {
            return new ServiceResponse<OrderDto>
            {
                Success = false,
                Message = "Không tìm thấy đơn hàng"
            };
        }

        return new ServiceResponse<OrderDto>
        {
            Success = true,
            Data = new OrderDto
            {
                Id = order.Id,
                OrderCode = order.OrderCode,
                CustomerId = order.CustomerId,
                CustomerName = order.Customer.FullName,
                CustomerEmail = await _authResolver.GetEmailByUserIdAsync(order.CustomerId),
                CustomerAvatarUrl = await _authResolver.GetAvatarUrlByUserIdAsync(order.CustomerId),
                CustomerPhone = order.Customer.Phone,
                TotalAmount = order.Total,
                Status = order.Status,
                CancelReason = order.CancelReason,
                ShippingAddress = order.ShippingAddress != null 
                    ? $"{order.ShippingAddress.AddressLine1}, {order.ShippingAddress.Ward}, {order.ShippingAddress.District}, {order.ShippingAddress.City}"
                    : order.ShipAddress,
                ProviderShippingFee = order.ProviderShippingFee,
                ShippingProvider = order.ShippingProvider,
                ShippingServiceId = order.ShippingServiceId,
                TrackingCode = order.TrackingCode,
                EstimatedDeliveryDate = order.EstimatedDeliveryDate,
                ActualDeliveryDate = order.ActualDeliveryDate,
                CreatedAt = order.CreatedAt,
                ShopGhnShopId = shop.GhnShopId,
                ShopFromDistrictId = shop.DistrictId,
                ShopFromWardCode = shop.WardCode,
                Items = order.OrderItems.Select(oi => new OrderItemDto
                {
                    Id = oi.Id,
                    ProductId = oi.ProductId,
                    ProductName = oi.Product.Name,
                    ProductThumbnailUrl = oi.Product.ProductImages
                        .OrderBy(pi => pi.SortOrder)
                        .Select(pi => pi.ImageUrl)
                        .FirstOrDefault(),
                    VariantName = oi.Variant?.VariantName,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    TotalPrice = oi.LineTotal
                }).ToList()
            }
        };
    }

    public async Task<ServiceResponse> UpdateOrderStatusAsync(Guid userId, Guid orderId, SellerUpdateOrderStatusDto dto)
    {
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.OwnerId == userId);

        if (shop == null)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = "Bạn chưa có shop"
            };
        }

        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.ShopId == shop.Id);

        if (order == null)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = "Không tìm thấy đơn hàng"
            };
        }

        var oldStatus = (OrderStatus)order.Status;
        var newOrderStatus = (OrderStatus)dto.Status;
        order.Status = dto.Status;
        if (newOrderStatus == OrderStatus.Cancelled)
        {
            order.CancelReason = string.IsNullOrWhiteSpace(dto.Note)
                ? null
                : dto.Note.Trim()[..Math.Min(dto.Note.Trim().Length, 500)];
        }
        order.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(dto.TrackingCode))
        {
            order.TrackingCode = dto.TrackingCode;
        }

        if (newOrderStatus == OrderStatus.Delivered && oldStatus != OrderStatus.Delivered)
        {
            order.ActualDeliveryDate = DateTime.UtcNow;
        }

        // Cộng SoldCount khi đơn lần đầu đạt Completed(6) — khách xác nhận nhận hàng
        var alreadyFulfilled = oldStatus == OrderStatus.Completed;
        var nowFulfilled = newOrderStatus == OrderStatus.Completed;
        if (nowFulfilled && !alreadyFulfilled)
        {
            await IncrementSoldCountAsync(order.OrderItems);
        }

        if (newOrderStatus == OrderStatus.Completed)
        {
            await _walletRelease.TryReleaseSettlementForOrderAsync(order.Id);
        }

        if (newOrderStatus is OrderStatus.Cancelled or OrderStatus.Refunded)
        {
            await _walletReversal.TryReverseSettlementForOrderAsync(
                order.Id,
                $"Seller đổi trạng thái → {newOrderStatus}");
        }

        await _context.SaveChangesAsync();

        await NotifyStatusChanged(order, oldStatus, (OrderStatus)order.Status);

        var newStatus = (OrderStatus)order.Status;
        var code = NotificationFormatting.ShortEntityId(order.Id);
        var composed = await _orderEmailComposer.TryComposeAsync(order.Id, oldStatus, newStatus);
        await _notifications.PublishAsync(
            order.CustomerId,
            nameof(NotificationType.Order),
            "Cập nhật trạng thái đơn hàng",
            $"Đơn #{code} chuyển từ {oldStatus} sang {newStatus}.",
            "Order",
            order.Id,
            queueEmail: true,
            emailHtmlBody: composed?.Html,
            emailSubjectOverride: composed?.Subject);

        return new ServiceResponse
        {
            Success = true,
            Message = "Cập nhật trạng thái đơn hàng thành công"
        };
    }

    public async Task<ServiceResponse<SellerProductReviewsDataDto>> GetMyProductReviewsAsync(
        Guid userId,
        int page,
        int pageSize,
        short? rating,
        string? search)
    {
        var shop = await _context.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.OwnerId == userId);
        if (shop == null)
        {
            return new ServiceResponse<SellerProductReviewsDataDto>
            {
                Success = false,
                Message = "Bạn chưa có shop"
            };
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var approved = (short)ReviewStatus.Approved;

        var baseQuery =
            from r in _context.ProductReviews.AsNoTracking()
            join p in _context.Products.AsNoTracking() on r.ProductId equals p.Id
            where p.ShopId == shop.Id && r.Status == approved
            join u in _context.Users.AsNoTracking() on r.UserId equals u.Id
            select new { r, p, u };

        if (rating is >= 1 and <= 5)
            baseQuery = baseQuery.Where(x => x.r.Rating == rating.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            if (term.Length > 200)
                term = term[..200];
            var pattern = "%" + term.Replace("%", "\\%").Replace("_", "\\_") + "%";
            baseQuery = baseQuery.Where(x =>
                (x.u.FullName != null && EF.Functions.ILike(x.u.FullName, pattern)) ||
                (x.r.Content != null && EF.Functions.ILike(x.r.Content, pattern)) ||
                EF.Functions.ILike(x.p.Name, pattern));
        }

        var totalCount = await baseQuery.CountAsync();

        var averageRating = 0.0;
        if (totalCount > 0)
            averageRating = Math.Round(await baseQuery.AverageAsync(x => (double)x.r.Rating), 1);

        var dist = new int[5];
        var groups = await baseQuery
            .GroupBy(x => x.r.Rating)
            .Select(g => new { Rating = g.Key, Cnt = g.Count() })
            .ToListAsync();
        foreach (var g in groups)
        {
            if (g.Rating is >= 1 and <= 5)
                dist[5 - g.Rating] = g.Cnt;
        }

        var items = await baseQuery
            .OrderByDescending(x => x.r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SellerProductReviewItemDto
            {
                Id = x.r.Id,
                ProductId = x.p.Id,
                ProductName = x.p.Name,
                ProductThumbnailUrl = _context.ProductImages
                    .Where(pi => pi.ProductId == x.p.Id)
                    .OrderBy(pi => pi.SortOrder)
                    .Select(pi => pi.ImageUrl)
                    .FirstOrDefault(),
                BuyerName = x.u.FullName,
                Rating = x.r.Rating,
                Comment = x.r.Content,
                SellerReply = x.r.SellerReply,
                CreatedAt = x.r.CreatedAt,
                ImageUrls = x.r.ImageUrls
            })
            .ToListAsync();

        return new ServiceResponse<SellerProductReviewsDataDto>
        {
            Success = true,
            Data = new SellerProductReviewsDataDto
            {
                Reviews = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                AverageRating = averageRating,
                RatingDistribution = dist,
                PendingReplyCount = await baseQuery.CountAsync(x => x.r.SellerReply == null || x.r.SellerReply == "")
            }
        };
    }

    public async Task<ServiceResponse> ReplyToReviewAsync(Guid userId, Guid reviewId, string reply)
    {
        var shop = await _context.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.OwnerId == userId);
        if (shop == null)
            return new ServiceResponse { Success = false, Message = "Bạn chưa có shop" };

        var review = await _context.ProductReviews
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.Product.ShopId == shop.Id);

        if (review == null)
            return new ServiceResponse { Success = false, Message = "Không tìm thấy đánh giá" };

        review.SellerReply = reply.Trim();
        review.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new ServiceResponse { Success = true, Message = "Phản hồi đã được lưu" };
    }

    private async Task IncrementSoldCountAsync(IEnumerable<OrderItem> items)
    {
        foreach (var item in items)
        {
            await _context.Products
                .Where(p => p.Id == item.ProductId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.SoldCount, p => p.SoldCount + item.Quantity));
        }
    }

    private async Task NotifyStatusChanged(Order order, OrderStatus oldStatus, OrderStatus newStatus)
    {
        var groupName = OrderTrackingHub.GetUserGroupName(order.CustomerId);

        await _hubContext.Clients.Group(groupName).SendAsync("OrderStatusUpdated", new
        {
            orderId = order.Id,
            oldStatus = (short)oldStatus,
            oldStatusName = oldStatus.ToString(),
            newStatus = (short)newStatus,
            newStatusName = newStatus.ToString(),
            updatedAt = order.UpdatedAt
        });
    }

    /// <summary>
    /// Cột <c>product_variants.attributes</c> là jsonb. Văn bản tự do (vd. &quot;Màu đỏ&quot;) không phải JSON — bọc thành chuỗi JSON hợp lệ.
    /// </summary>
    private static string? NormalizeVariantAttributesForJsonb(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var t = raw.Trim();
        try
        {
            using (JsonDocument.Parse(t))
                return t;
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(t);
        }
    }

    /// <summary>
    /// Sinh mã PRD##### duy nhất. Sequence PostgreSQL có thể lệch so với dữ liệu (import / seed),
    /// nên luôn đối chiếu MAX trên bảng và đồng bộ lại sequence sau khi chọn mã.
    /// </summary>
    private async Task<string> GenerateUniqueProductCodeAsync(CancellationToken cancellationToken = default)
    {
        // Không dùng regex dạng {5} trong chuỗi — EF SqlQueryRaw gọi string.Format và coi {n} là placeholder.
        const string sqlMaxNumeric = """
            SELECT COALESCE(MAX(CAST(SUBSTRING(p.product_code FROM 4) AS BIGINT)), 0) AS "Value"
            FROM products AS p
            WHERE length(p.product_code) = 8
              AND p.product_code LIKE 'PRD%'
              AND translate(substring(p.product_code FROM 4), '0123456789', '') = ''
            """;

        var maxNumeric = await _context.Database
            .SqlQueryRaw<long>(sqlMaxNumeric)
            .FirstAsync(cancellationToken);

        var seqVal = await _context.Database
            .SqlQueryRaw<long>("SELECT nextval('products_code_seq') AS \"Value\"")
            .FirstAsync(cancellationToken);

        var candidate = Math.Max(maxNumeric + 1, seqVal);

        for (var attempt = 0; attempt < 500; attempt++)
        {
            var code = $"PRD{candidate:D5}";
            if (!await _context.Products.AnyAsync(p => p.ProductCode == code, cancellationToken))
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"""SELECT setval('products_code_seq', {candidate}, true)""",
                    cancellationToken);
                return code;
            }

            candidate++;
        }

        throw new InvalidOperationException("Không thể sinh mã sản phẩm PRD duy nhất.");
    }

    private async Task<string> GenerateUniqueProductSlugAsync(string productName, Guid? excludeProductId = null)
    {
        var baseSlug = GenerateSlug(productName);
        var slug = baseSlug;
        var suffix = 1;

        while (await _context.Products.AnyAsync(p => p.Slug == slug && (!excludeProductId.HasValue || p.Id != excludeProductId.Value)))
        {
            slug = $"{baseSlug}-{suffix++}";
        }

        return slug;
    }

    private static string GenerateSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "product";

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        var withoutDiacritics = builder.ToString().Normalize(NormalizationForm.FormC);
        var slug = Regex.Replace(withoutDiacritics, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        slug = slug.Trim('-');

        return string.IsNullOrWhiteSpace(slug) ? "product" : slug;
    }
}
