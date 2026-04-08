using System.Text;
using System.Text.Json;
using ECommerceAPI.Application.DTOs.User;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class UserProfileService : IUserProfileService
{
    private readonly ApplicationDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly IUserAuthEmailResolver _authEmailResolver;
    private readonly IEmailService _emailService;
    private readonly IOtpService _otpService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserProfileService> _logger;

    public UserProfileService(
        ApplicationDbContext context,
        IUserRepository userRepository,
        IUserAuthEmailResolver authEmailResolver,
        IEmailService emailService,
        IOtpService otpService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<UserProfileService> logger)
    {
        _context = context;
        _userRepository = userRepository;
        _authEmailResolver = authEmailResolver;
        _emailService = emailService;
        _otpService = otpService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<UserProfileResponse> GetProfileAsync(Guid userId)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.ShopOwners)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            throw new Exception("User not found");
        }

        var shop = user.ShopOwners.FirstOrDefault();
        var authEmail = await _authEmailResolver.GetEmailByUserIdAsync(userId);

        return new UserProfileResponse
        {
            Id = user.Id,
            Email = authEmail,
            FullName = user.FullName,
            Phone = user.Phone,
            Role = user.Role?.Code ?? string.Empty,
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            Shop = shop != null ? new ShopInfoDto
            {
                Id = shop.Id,
                Name = shop.Name,
                Description = shop.Description,
                Phone = shop.Phone,
                AddressLine = shop.AddressLine,
                WardCode = shop.WardCode,
                DistrictId = shop.DistrictId,
                ProvinceId = shop.ProvinceId,
                City = shop.City,
                GhnShopId = shop.GhnShopId,
                Status = shop.Status,
                VerificationStatus = shop.VerificationStatus
            } : null
        };
    }

    public async Task<ServiceResponse> UpdateProfileAsync(Guid userId, UpdateProfileDto dto)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = "User not found"
            };
        }

        if (!string.IsNullOrEmpty(dto.FullName))
            user.FullName = dto.FullName;

        if (!string.IsNullOrEmpty(dto.Phone))
            user.Phone = dto.Phone;

        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);

        return new ServiceResponse
        {
            Success = true,
            Message = "Cập nhật profile thành công"
        };
    }

    public async Task<ServiceResponse> RegisterAsSellerAsync(Guid userId, RegisterSellerDto dto)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.ShopOwners)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = "User not found"
            };
        }

        if (user.Role?.Code == "seller" || user.Role?.Code == "admin")
        {
            return new ServiceResponse
            {
                Success = false,
                Message = "Bạn đã là seller hoặc admin"
            };
        }

        if (user.ShopOwners.Any())
        {
            return new ServiceResponse
            {
                Success = false,
                Message = "Bạn đã có shop rồi"
            };
        }

        var slug = GenerateSlug(dto.ShopName);
        var existingShop = await _context.Shops.FirstOrDefaultAsync(s => s.Slug == slug);
        if (existingShop != null)
        {
            slug = $"{slug}-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        var seqValue = await _context.Database
            .SqlQueryRaw<long>("SELECT nextval('shops_code_seq')")
            .FirstAsync();
        var shopCode = $"SH-{DateTime.UtcNow:yyyyMM}-{seqValue:D5}";

        var shop = new Shop
        {
            Id = Guid.NewGuid(),
            ShopCode = shopCode,
            OwnerId = userId,
            Name = dto.ShopName,
            Slug = slug,
            Description = dto.ShopDescription,
            Phone = dto.Phone,
            AddressLine = dto.AddressLine,
            WardCode = dto.WardCode,
            DistrictId = dto.DistrictId,
            ProvinceId = dto.ProvinceId,
            City = dto.City,
            Status = 0, // Inactive until approved
            VerificationStatus = 0, // Pending
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Shops.Add(shop);

        // Note: ShopDocument entity chỉ có DocType, FileUrl, Status
        // Business info sẽ được store ở table khác hoặc mở rộng ShopDocument entity
        // Tạm thời lưu thông tin business info vào shop description
        var businessInfo = $@"
Business License: {dto.BusinessLicenseNumber}
Tax Code: {dto.TaxCode}
Business Type: {dto.BusinessType}
Bank: {dto.BankName}
Account Number: {dto.BankAccountNumber}
Account Name: {dto.BankAccountName}
";

        shop.Description = (shop.Description ?? "") + "\n\n" + businessInfo;

        await _context.SaveChangesAsync();

        return new ServiceResponse
        {
            Success = true,
            Message = "Đăng ký seller thành công. Vui lòng chờ admin phê duyệt."
        };
    }

    public async Task<List<AddressDto>> GetAddressesAsync(Guid userId)
    {
        var addresses = await _context.Addresses
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new AddressDto
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

        return addresses;
    }

    public async Task<ServiceResponse<AddressDto>> AddAddressAsync(Guid userId, AddAddressDto dto)
    {
        if (dto.IsDefault)
        {
            var existingAddresses = await _context.Addresses
                .Where(a => a.UserId == userId)
                .ToListAsync();

            foreach (var addr in existingAddresses)
            {
                addr.IsDefault = false;
                addr.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        var address = new Address
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Label = dto.Label,
            FullName = dto.FullName,
            Phone = dto.Phone,
            AddressLine1 = dto.AddressLine1,
            AddressLine2 = dto.AddressLine2,
            Ward = dto.Ward,
            District = dto.District,
            City = dto.City,
            Province = dto.Province,
            PostalCode = dto.PostalCode,
            Country = dto.Country,
            IsDefault = dto.IsDefault,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Addresses.Add(address);
        await _context.SaveChangesAsync();

        return new ServiceResponse<AddressDto>
        {
            Success = true,
            Message = "Thêm địa chỉ thành công",
            Data = new AddressDto
            {
                Id = address.Id,
                Label = address.Label,
                FullName = address.FullName,
                Phone = address.Phone,
                AddressLine1 = address.AddressLine1,
                AddressLine2 = address.AddressLine2,
                Ward = address.Ward,
                District = address.District,
                City = address.City,
                Province = address.Province,
                PostalCode = address.PostalCode,
                Country = address.Country,
                IsDefault = address.IsDefault,
                CreatedAt = address.CreatedAt
            }
        };
    }

    public async Task<ServiceResponse> UpdateAddressAsync(Guid userId, Guid addressId, UpdateAddressDto dto)
    {
        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId);

        if (address == null)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = "Không tìm thấy địa chỉ"
            };
        }

        if (dto.IsDefault == true)
        {
            var otherAddresses = await _context.Addresses
                .Where(a => a.UserId == userId && a.Id != addressId)
                .ToListAsync();

            foreach (var addr in otherAddresses)
            {
                addr.IsDefault = false;
                addr.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        if (!string.IsNullOrEmpty(dto.Label))
            address.Label = dto.Label;
        if (!string.IsNullOrEmpty(dto.FullName))
            address.FullName = dto.FullName;
        if (!string.IsNullOrEmpty(dto.Phone))
            address.Phone = dto.Phone;
        if (!string.IsNullOrEmpty(dto.AddressLine1))
            address.AddressLine1 = dto.AddressLine1;
        if (dto.AddressLine2 != null)
            address.AddressLine2 = dto.AddressLine2;
        if (dto.Ward != null)
            address.Ward = dto.Ward;
        if (dto.District != null)
            address.District = dto.District;
        if (!string.IsNullOrEmpty(dto.City))
            address.City = dto.City;
        if (dto.Province != null)
            address.Province = dto.Province;
        if (dto.PostalCode != null)
            address.PostalCode = dto.PostalCode;
        if (!string.IsNullOrEmpty(dto.Country))
            address.Country = dto.Country;
        if (dto.IsDefault.HasValue)
            address.IsDefault = dto.IsDefault.Value;

        address.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new ServiceResponse
        {
            Success = true,
            Message = "Cập nhật địa chỉ thành công"
        };
    }

    public async Task<ServiceResponse> DeleteAddressAsync(Guid userId, Guid addressId)
    {
        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId);

        if (address == null)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = "Không tìm thấy địa chỉ"
            };
        }

        _context.Addresses.Remove(address);
        await _context.SaveChangesAsync();

        return new ServiceResponse
        {
            Success = true,
            Message = "Xóa địa chỉ thành công"
        };
    }

    public async Task<ServiceResponse> SetDefaultAddressAsync(Guid userId, Guid addressId)
    {
        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId);

        if (address == null)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = "Không tìm thấy địa chỉ"
            };
        }

        var otherAddresses = await _context.Addresses
            .Where(a => a.UserId == userId && a.Id != addressId)
            .ToListAsync();

        foreach (var addr in otherAddresses)
        {
            addr.IsDefault = false;
            addr.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        address.IsDefault = true;
        address.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new ServiceResponse
        {
            Success = true,
            Message = "Đặt địa chỉ mặc định thành công"
        };
    }

    public async Task<ServiceResponse> RequestEmailChangeAsync(Guid userId, string currentEmail, RequestEmailChangeDto dto)
    {
        var newEmail = dto.NewEmail.Trim().ToLowerInvariant();

        if (newEmail == currentEmail.Trim().ToLowerInvariant())
            return new ServiceResponse { Success = false, Message = "Email mới phải khác email hiện tại" };

        var otp = _otpService.GenerateAndStore(userId, newEmail);

        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:480px;margin:0 auto;padding:24px">
              <h2 style="color:#ec7f13;margin-bottom:8px">Xác nhận thay đổi Email</h2>
              <p>Mã OTP xác nhận thay đổi email của bạn là:</p>
              <div style="font-size:36px;font-weight:bold;letter-spacing:10px;color:#ec7f13;padding:16px 0">{otp}</div>
              <p style="color:#666;font-size:13px">Mã có hiệu lực trong <strong>10 phút</strong>. Không chia sẻ mã này với bất kỳ ai.</p>
            </div>
            """;

        try
        {
            await _emailService.SendAsync(newEmail, "Mã OTP thay đổi Email", html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OTP email to {Email}", newEmail);
            return new ServiceResponse { Success = false, Message = $"Không thể gửi email: {ex.Message}" };
        }

        return new ServiceResponse { Success = true, Message = "Đã gửi mã OTP đến email mới" };
    }

    public async Task<ServiceResponse> ConfirmEmailChangeAsync(Guid userId, ConfirmEmailChangeDto dto)
    {
        var newEmail = dto.NewEmail.Trim().ToLowerInvariant();

        if (!_otpService.Verify(userId, newEmail, dto.Otp))
            return new ServiceResponse { Success = false, Message = "Mã OTP không đúng hoặc đã hết hạn" };

        // Gọi Supabase Admin API để cập nhật email
        var supabaseUrl = _configuration["Supabase:Url"]!;
        var serviceRoleKey = _configuration["Supabase:ServiceRoleKey"]!;

        using var http = _httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Add("apikey", serviceRoleKey);
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {serviceRoleKey}");

        var body = JsonSerializer.Serialize(new { email = newEmail, email_confirm = true });
        var response = await http.PutAsync(
            $"{supabaseUrl}/auth/v1/admin/users/{userId}",
            new StringContent(body, Encoding.UTF8, "application/json")
        );

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            _logger.LogError("Supabase Admin API error ({Status}): {Error}", response.StatusCode, err);
            return new ServiceResponse
            {
                Success = false,
                Message = $"Không thể cập nhật email. Supabase trả về: {(int)response.StatusCode} - {err}"
            };
        }

        return new ServiceResponse { Success = true, Message = "Email đã được cập nhật thành công" };
    }

    private string GenerateSlug(string name)
    {
        var slug = name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("đ", "d")
            .Replace("á", "a").Replace("à", "a").Replace("ả", "a").Replace("ã", "a").Replace("ạ", "a")
            .Replace("ă", "a").Replace("ắ", "a").Replace("ằ", "a").Replace("ẳ", "a").Replace("ẵ", "a").Replace("ặ", "a")
            .Replace("â", "a").Replace("ấ", "a").Replace("ầ", "a").Replace("ẩ", "a").Replace("ẫ", "a").Replace("ậ", "a")
            .Replace("é", "e").Replace("è", "e").Replace("ẻ", "e").Replace("ẽ", "e").Replace("ẹ", "e")
            .Replace("ê", "e").Replace("ế", "e").Replace("ề", "e").Replace("ể", "e").Replace("ễ", "e").Replace("ệ", "e")
            .Replace("í", "i").Replace("ì", "i").Replace("ỉ", "i").Replace("ĩ", "i").Replace("ị", "i")
            .Replace("ó", "o").Replace("ò", "o").Replace("ỏ", "o").Replace("õ", "o").Replace("ọ", "o")
            .Replace("ô", "o").Replace("ố", "o").Replace("ồ", "o").Replace("ổ", "o").Replace("ỗ", "o").Replace("ộ", "o")
            .Replace("ơ", "o").Replace("ớ", "o").Replace("ờ", "o").Replace("ở", "o").Replace("ỡ", "o").Replace("ợ", "o")
            .Replace("ú", "u").Replace("ù", "u").Replace("ủ", "u").Replace("ũ", "u").Replace("ụ", "u")
            .Replace("ư", "u").Replace("ứ", "u").Replace("ừ", "u").Replace("ử", "u").Replace("ữ", "u").Replace("ự", "u")
            .Replace("ý", "y").Replace("ỳ", "y").Replace("ỷ", "y").Replace("ỹ", "y").Replace("ỵ", "y");

        slug = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

        return slug;
    }
}
