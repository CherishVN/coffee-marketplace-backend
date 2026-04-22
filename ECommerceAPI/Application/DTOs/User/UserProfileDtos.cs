using FluentValidation;

namespace ECommerceAPI.Application.DTOs.User;

public class UpdateProfileDto
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
}

public class RegisterSellerDto
{
    public string ShopName { get; set; } = string.Empty;
    public string? ShopDescription { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine { get; set; }
    public string? WardCode { get; set; }
    public int? DistrictId { get; set; }
    public int? ProvinceId { get; set; }
    public string? City { get; set; }
    public string? BusinessLicenseNumber { get; set; }
    public string? TaxCode { get; set; }
    public string BusinessType { get; set; } = string.Empty;
    public long PrimaryCategoryId { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankAccountName { get; set; }
    public SellerIdentityInfoDto? Identity { get; set; }
    public List<ShopDocumentInputDto>? Documents { get; set; }
}

public class SellerIdentityInfoDto
{
    public string? FullName { get; set; }
    public string? IdNumber { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Sex { get; set; }
    public string? Nationality { get; set; }
    public string? HomeTown { get; set; }
    public string? PermanentAddress { get; set; }
    public string? AddrProvince { get; set; }
    public string? AddrDistrict { get; set; }
    public string? AddrWard { get; set; }
    public string? AddrStreet { get; set; }
    public string? DateOfExpiry { get; set; }
    public string? CardType { get; set; }
    public string? IssueDate { get; set; }
    public string? IssuePlace { get; set; }
    public string? Religion { get; set; }
    public string? Ethnicity { get; set; }
    public string? Features { get; set; }
}

public class ShopDocumentInputDto
{
    public string DocType { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
}

public class AddAddressDto
{
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
    public string Country { get; set; } = "Vietnam";
    public bool IsDefault { get; set; }
}

public class UpdateAddressDto
{
    public string? Label { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Ward { get; set; }
    public string? District { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public bool? IsDefault { get; set; }
}

public class RequestEmailChangeDto
{
    public string NewEmail { get; set; } = string.Empty;
}

public class ConfirmEmailChangeDto
{
    public string NewEmail { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
}

// Validators
public class UpdateProfileDtoValidator : AbstractValidator<UpdateProfileDto>
{
    public UpdateProfileDtoValidator()
    {
        RuleFor(x => x.FullName)
            .MaximumLength(255).WithMessage("Họ tên không được vượt quá 255 ký tự")
            .When(x => !string.IsNullOrEmpty(x.FullName));

        RuleFor(x => x.Phone)
            .Matches(@"^(0|\+84)[0-9]{9,10}$").WithMessage("Số điện thoại không hợp lệ")
            .When(x => !string.IsNullOrEmpty(x.Phone));
    }
}

public class RegisterSellerDtoValidator : AbstractValidator<RegisterSellerDto>
{
    public RegisterSellerDtoValidator()
    {
        RuleFor(x => x.ShopName)
            .NotEmpty().WithMessage("Tên shop không được để trống")
            .MaximumLength(255).WithMessage("Tên shop không được vượt quá 255 ký tự");

        RuleFor(x => x.ShopDescription)
            .MaximumLength(2000).WithMessage("Mô tả shop không được vượt quá 2000 ký tự")
            .When(x => !string.IsNullOrEmpty(x.ShopDescription));

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Số điện thoại không được để trống")
            .Matches(@"^(0|\+84)[0-9]{9,10}$").WithMessage("Số điện thoại không hợp lệ");

        RuleFor(x => x.AddressLine)
            .NotEmpty().WithMessage("Địa chỉ không được để trống")
            .MaximumLength(500).WithMessage("Địa chỉ không được vượt quá 500 ký tự");

        RuleFor(x => x.WardCode)
            .NotEmpty().WithMessage("Mã phường/xã không được để trống")
            .MaximumLength(50).WithMessage("Mã phường/xã không được vượt quá 50 ký tự");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("Tên thành phố không được để trống")
            .MaximumLength(100).WithMessage("Tên thành phố không được vượt quá 100 ký tự");

        RuleFor(x => x.DistrictId)
            .NotNull().WithMessage("district_id không được để trống")
            .GreaterThan(0).WithMessage("district_id phải lớn hơn 0");

        RuleFor(x => x.ProvinceId)
            .NotNull().WithMessage("province_id không được để trống")
            .GreaterThan(0).WithMessage("province_id phải lớn hơn 0");

        RuleFor(x => x.BusinessType)
            .NotEmpty().WithMessage("Loại hình kinh doanh không được để trống")
            .Must(x => new[] { "individual", "company", "household" }.Contains(x))
            .WithMessage("Loại hình kinh doanh không hợp lệ (individual, company, household)");

        RuleFor(x => x.PrimaryCategoryId)
            .GreaterThan(0).WithMessage("Vui lòng chọn một ngành hàng (danh mục gốc) để bán");

        RuleFor(x => x.Identity)
            .NotNull()
            .WithMessage("Vui lòng điền thông tin định danh từ CCCD (họ tên tối thiểu)");

        RuleFor(x => x.Identity!.FullName)
            .NotEmpty()
            .WithMessage("Họ tên theo CCCD không được để trống")
            .MaximumLength(255)
            .When(x => x.Identity != null);

        RuleFor(x => x.TaxCode)
            .Matches(@"^[0-9]{10}(-[0-9]{3})?$").WithMessage("Mã số thuế không hợp lệ")
            .When(x => !string.IsNullOrEmpty(x.TaxCode));
    }
}

public class RequestEmailChangeDtoValidator : AbstractValidator<RequestEmailChangeDto>
{
    public RequestEmailChangeDtoValidator()
    {
        RuleFor(x => x.NewEmail)
            .NotEmpty().WithMessage("Email không được để trống")
            .EmailAddress().WithMessage("Email không hợp lệ");
    }
}

public class ConfirmEmailChangeDtoValidator : AbstractValidator<ConfirmEmailChangeDto>
{
    public ConfirmEmailChangeDtoValidator()
    {
        RuleFor(x => x.NewEmail)
            .NotEmpty().WithMessage("Email không được để trống")
            .EmailAddress().WithMessage("Email không hợp lệ");

        RuleFor(x => x.Otp)
            .NotEmpty().WithMessage("Mã OTP không được để trống")
            .Matches(@"^\d{6}$").WithMessage("Mã OTP phải là 6 chữ số");
    }
}

public class AddAddressDtoValidator : AbstractValidator<AddAddressDto>
{
    public AddAddressDtoValidator()
    {
        RuleFor(x => x.AddressLine1)
            .NotEmpty().WithMessage("Địa chỉ không được để trống")
            .MaximumLength(500).WithMessage("Địa chỉ không được vượt quá 500 ký tự");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("Thành phố không được để trống")
            .MaximumLength(100).WithMessage("Thành phố không được vượt quá 100 ký tự");

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Quốc gia không được để trống")
            .MaximumLength(100).WithMessage("Quốc gia không được vượt quá 100 ký tự");

        RuleFor(x => x.Phone)
            .Matches(@"^(0|\+84)[0-9]{9,10}$").WithMessage("Số điện thoại không hợp lệ")
            .When(x => !string.IsNullOrEmpty(x.Phone));
    }
}
