using ECommerceAPI.Application.DTOs.User;

namespace ECommerceAPI.Application.Interfaces;

public interface IUserProfileService
{
    Task<UserProfileResponse> GetProfileAsync(Guid userId);
    Task<ServiceResponse> UpdateProfileAsync(Guid userId, UpdateProfileDto dto);
    Task<ServiceResponse> RegisterAsSellerAsync(Guid userId, RegisterSellerDto dto);
    Task<List<AddressDto>> GetAddressesAsync(Guid userId);
    Task<ServiceResponse<AddressDto>> AddAddressAsync(Guid userId, AddAddressDto dto);
    Task<ServiceResponse> UpdateAddressAsync(Guid userId, Guid addressId, UpdateAddressDto dto);
    Task<ServiceResponse> DeleteAddressAsync(Guid userId, Guid addressId);
    Task<ServiceResponse> SetDefaultAddressAsync(Guid userId, Guid addressId);
    Task<ServiceResponse> RequestEmailChangeAsync(Guid userId, string currentEmail, RequestEmailChangeDto dto);
    Task<ServiceResponse> ConfirmEmailChangeAsync(Guid userId, ConfirmEmailChangeDto dto);
}

public class UserProfileResponse
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
    public short Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public ShopInfoDto? Shop { get; set; }
}

public class ShopInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine { get; set; }
    public string? WardCode { get; set; }
    public int? DistrictId { get; set; }
    public int? ProvinceId { get; set; }
    public string? City { get; set; }
    public int? GhnShopId { get; set; }
    public short Status { get; set; }
    public short VerificationStatus { get; set; }
}

public class AddressDto
{
    public Guid Id { get; set; }
    public string? Label { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string? Ward { get; set; }
    public string? District { get; set; }
    public string City { get; set; } = string.Empty;
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ServiceResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ServiceResponse<T> : ServiceResponse
{
    public T? Data { get; set; }
    public int TotalCount { get; set; }
}
