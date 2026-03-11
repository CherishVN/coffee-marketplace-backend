using ECommerceAPI.Application.DTOs.Cart;

namespace ECommerceAPI.Application.Interfaces;

public interface ICartService
{
    Task<CartDto?> GetMyCartAsync(Guid customerId);
    Task<(bool Success, string? Error, CartItemDto? Item)> AddItemAsync(Guid customerId, AddCartItemDto dto);
    Task<(bool Success, string? Error)> UpdateItemAsync(Guid customerId, Guid itemId, UpdateCartItemDto dto);
    Task<(bool Success, string? Error)> RemoveItemAsync(Guid customerId, Guid itemId);
    Task<CheckoutResponseDto> CheckoutAsync(Guid customerId, CheckoutDto dto);
}
