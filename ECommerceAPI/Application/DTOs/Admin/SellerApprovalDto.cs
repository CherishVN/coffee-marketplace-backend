using ECommerceAPI.Application.DTOs.User;

namespace ECommerceAPI.Application.DTOs.Admin;

public class ShopVerificationDto
{
    public Guid Id { get; set; }
    public string ShopCode { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerEmail { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine { get; set; }
    public string? WardCode { get; set; }
    public int? DistrictId { get; set; }
    public int? ProvinceId { get; set; }
    public string? City { get; set; }
    public int? GhnShopId { get; set; }
    public string? BusinessType { get; set; }
    public string? BusinessLicenseNumber { get; set; }
    public string? TaxCode { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankAccountName { get; set; }
    public short Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public short VerificationStatus { get; set; }
    public string VerificationStatusName { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public Guid? VerifiedBy { get; set; }
    public string? VerifiedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ShopDocumentDto> Documents { get; set; } = new();
    /// <summary>Snapshot nội dung CCCD/CMND (JSON) từ đăng ký — không có URL ảnh thẻ.</summary>
    public SellerIdentityInfoDto? Identity { get; set; }
}

public class ShopDocumentDto
{
    public Guid Id { get; set; }
    public string DocType { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public short Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByName { get; set; }
}

public class ApproveSellerDto
{
    public string? Note { get; set; }
}

public class RejectSellerDto
{
    public string Reason { get; set; } = string.Empty;
}

public class ShopListResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<ShopVerificationDto> Shops { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class ShopResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public ShopVerificationDto? Shop { get; set; }
}

public class ChangeShopStatusDto
{
    public short Status { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class SuspendShopDto
{
    public string Reason { get; set; } = string.Empty;
}

public class ShopStatusDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public short Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int ActiveOrdersCount { get; set; }
    public int ProductsCount { get; set; }
    public string? SuspensionReason { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public string? SuspendedByName { get; set; }
}
