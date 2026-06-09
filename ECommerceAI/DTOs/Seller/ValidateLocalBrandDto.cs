namespace ECommerceAI.DTOs.Seller;

public class ValidateLocalBrandRequestDto
{
    /// <summary>Tên sản phẩm cần xác thực</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Mô tả sản phẩm</summary>
    public string? Description { get; set; }

    /// <summary>Tên vùng xuất xứ đã đăng ký (VD: "Đắk Lắk")</summary>
    public string ProvinceName { get; set; } = string.Empty;

    /// <summary>Tên archetype profile (VD: "Robusta Buôn Ma Thuột")</summary>
    public string ArchetypeName { get; set; } = string.Empty;
}

public class ValidateLocalBrandResponseDto
{
    public bool Success { get; set; }

    /// <summary>AI đánh giá sản phẩm có hợp lệ để đăng ký Local Brand không</summary>
    public bool IsValid { get; set; }

    /// <summary>Độ tin cậy của kết quả AI (0.0 - 1.0)</summary>
    public double Confidence { get; set; }

    /// <summary>Lý do ngắn gọn từ AI</summary>
    public string Reason { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}
