namespace ECommerceAPI.Application.DTOs.Admin;

public class AdminUserAddressDto
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

public class AdminWalletLedgerEntryDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminCustomerWalletDetailDto
{
    public Guid? WalletId { get; set; }
    public decimal AvailableBalance { get; set; }
    public string Currency { get; set; } = "VND";
    public List<AdminWalletLedgerEntryDto> Ledger { get; set; } = new();
}

public class AdminSellerWalletDetailDto
{
    public Guid? WalletId { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal HeldBalance { get; set; }
    public decimal PendingBalance { get; set; }
    public string Currency { get; set; } = "VND";
    public List<AdminWalletLedgerEntryDto> Ledger { get; set; } = new();
}

public class AdminUserProductReviewDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public short Rating { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public short Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminUserShopReviewDto
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public short Rating { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public short Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserAddressesResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<AdminUserAddressDto> Addresses { get; set; } = new();
}

public class UserWalletDetailResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public AdminCustomerWalletDetailDto? Customer { get; set; }
    public AdminSellerWalletDetailDto? Seller { get; set; }
}

public class UserProductReviewsResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<AdminUserProductReviewDto> Reviews { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class UserShopReviewsResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<AdminUserShopReviewDto> Reviews { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class SimpleMessageResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

public class UpdateUserAccountStatusDto
{
    /// <summary>0=Inactive, 1=Active, 2=Suspended (khóa có lý do)</summary>
    public short Status { get; set; }
    public string? Reason { get; set; }
}
