using ECommerceAPI.Application.DTOs.Seller;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Hubs;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class SellerService : ISellerService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<OrderTrackingHub> _hubContext;

    public SellerService(ApplicationDbContext context, IHubContext<OrderTrackingHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
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
                Name = shop.Name,
                Slug = shop.Slug,
                Description = shop.Description,
                LogoUrl = shop.LogoUrl,
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
        var totalWithdrawn = ledgers.Where(l => l.Amount < 0).Sum(l => Math.Abs(l.Amount));

        return new ServiceResponse<WalletDto>
        {
            Success = true,
            Data = new WalletDto
            {
                Id = wallet.Id,
                AvailableBalance = wallet.AvailableBalance,
                PendingBalance = wallet.PendingBalance,
                TotalEarnings = totalEarnings,
                TotalWithdrawn = totalWithdrawn,
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

        var product = new Product
        {
            Id = Guid.NewGuid(),
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
                    Attributes = variantDto.Attributes
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
                Quantity = 0,
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

        // Add materials (skip for now - need Material entity with Guid)
        // if (dto.MaterialIds != null && dto.MaterialIds.Any())
        // {
        //     foreach (var materialId in dto.MaterialIds)
        //     {
        //         var productMaterial = new ProductMaterial
        //         {
        //             ProductId = product.Id,
        //             MaterialId = materialId
        //         };
        //         _context.Set<ProductMaterial>().Add(productMaterial);
        //     }
        // }

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

        if (!string.IsNullOrEmpty(dto.Name))
            product.Name = dto.Name;

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

        // Soft delete - set status to deleted
        product.Status = 3; // Deleted
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new ServiceResponse
        {
            Success = true,
            Message = "Xóa sản phẩm thành công"
        };
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

    public async Task<ServiceResponse<List<OrderDto>>> GetMyOrdersAsync(Guid userId, int page, int pageSize, short? status)
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
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Variant)
            .Where(o => o.ShopId == shop.Id);

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderDto
            {
                Id = o.Id,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer.FullName,
                CustomerPhone = o.Customer.Phone,
                TotalAmount = o.Total,
                Status = o.Status,
                ShippingAddress = o.ShippingAddress != null 
                    ? $"{o.ShippingAddress.AddressLine1}, {o.ShippingAddress.Ward}, {o.ShippingAddress.District}, {o.ShippingAddress.City}"
                    : o.ShipAddress,
                CreatedAt = o.CreatedAt,
                Items = o.OrderItems.Select(oi => new OrderItemDto
                {
                    Id = oi.Id,
                    ProductId = oi.ProductId,
                    ProductName = oi.Product.Name,
                    VariantName = oi.Variant != null ? oi.Variant.VariantName : null,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    TotalPrice = oi.LineTotal
                }).ToList()
            })
            .ToListAsync();

        return new ServiceResponse<List<OrderDto>>
        {
            Success = true,
            Data = orders
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
                CustomerId = order.CustomerId,
                CustomerName = order.Customer.FullName,
                CustomerPhone = order.Customer.Phone,
                TotalAmount = order.Total,
                Status = order.Status,
                ShippingAddress = order.ShippingAddress != null 
                    ? $"{order.ShippingAddress.AddressLine1}, {order.ShippingAddress.Ward}, {order.ShippingAddress.District}, {order.ShippingAddress.City}"
                    : order.ShipAddress,
                CreatedAt = order.CreatedAt,
                Items = order.OrderItems.Select(oi => new OrderItemDto
                {
                    Id = oi.Id,
                    ProductId = oi.ProductId,
                    ProductName = oi.Product.Name,
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
        order.Status = dto.Status;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await NotifyStatusChanged(order, oldStatus, (OrderStatus)order.Status);

        return new ServiceResponse
        {
            Success = true,
            Message = "Cập nhật trạng thái đơn hàng thành công"
        };
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
}
