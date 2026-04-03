using ECommerceAPI.Application.DTOs.Seller;
using ECommerceAPI.Application.Interfaces;

namespace ECommerceAPI.Application.Interfaces;

public interface ISellerService
{
    // Shop Management
    Task<ServiceResponse<ShopDto>> GetMyShopAsync(Guid userId);
    Task<ServiceResponse> UpdateShopAsync(Guid userId, UpdateShopDto dto);
    
    // Wallet & Withdrawal Management
    Task<ServiceResponse<WalletDto>> GetMyWalletAsync(Guid userId);
    Task<ServiceResponse<List<WithdrawalRequestDto>>> GetMyWithdrawalRequestsAsync(Guid userId, int page, int pageSize);
    Task<ServiceResponse<WithdrawalRequestDto>> CreateWithdrawalRequestAsync(Guid userId, CreateWithdrawalRequestDto dto);
    
    // Product Management
    Task<ServiceResponse<List<ProductDto>>> GetMyProductsAsync(Guid userId, int page, int pageSize, short? status, string? search = null);
    Task<ServiceResponse<ProductDto>> GetProductByIdAsync(Guid userId, Guid productId);
    Task<ServiceResponse<ProductDto>> CreateProductAsync(Guid userId, CreateProductDto dto);
    Task<ServiceResponse> UpdateProductAsync(Guid userId, Guid productId, UpdateProductDto dto);
    Task<ServiceResponse> DeleteProductAsync(Guid userId, Guid productId);

    /// <summary>Thêm biến thể sau khi đã tạo sản phẩm (kèm dòng tồn kho).</summary>
    Task<ServiceResponse<ProductVariantDetailDto>> AddProductVariantAsync(Guid userId, Guid productId, ProductVariantDto dto);
    
    // Inventory Management
    Task<ServiceResponse> UpdateInventoryAsync(Guid userId, Guid productId, UpdateInventoryDto dto);
    
    // Order Management
    Task<ServiceResponse<List<OrderDto>>> GetMyOrdersAsync(Guid userId, int page, int pageSize, short? status);
    Task<ServiceResponse<OrderDto>> GetOrderByIdAsync(Guid userId, Guid orderId);
    Task<ServiceResponse> UpdateOrderStatusAsync(Guid userId, Guid orderId, SellerUpdateOrderStatusDto dto);

    /// <summary>Đánh giá sản phẩm của khách hàng đối với sản phẩm thuộc shop.</summary>
    Task<ServiceResponse<SellerProductReviewsDataDto>> GetMyProductReviewsAsync(
        Guid userId,
        int page,
        int pageSize,
        short? rating,
        string? search);
}
