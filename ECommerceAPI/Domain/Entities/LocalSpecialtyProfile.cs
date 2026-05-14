using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

/// <summary>
/// Bảng chuẩn "hồ sơ đặc sản địa phương" — mỗi dòng định nghĩa
/// một loại sản phẩm đặc trưng của một vùng (ví dụ: Robusta Buôn Ma Thuột / Đắk Lắk).
/// Admin quản lý; seller không tự thêm được.
/// </summary>
public class LocalSpecialtyProfile
{
    public int Id { get; set; }

    /// <summary>Code danh mục cha để lọc (vd: "ca_phe", "thu_cong").</summary>
    public string CategoryCode { get; set; } = null!;

    /// <summary>Tên tỉnh / vùng trồng (vd: "Đắk Lắk").</summary>
    public string ProvinceName { get; set; } = null!;

    /// <summary>Tên thương hiệu / loại đặc sản (vd: "Robusta Buôn Ma Thuột").</summary>
    public string ArchetypeName { get; set; } = null!;

    /// <summary>
    /// Danh sách đặc điểm tượng trưng (vd: ["Đắng đậm","Ít chua","Caffeine cao","Mùi chocolate"]).
    /// Lưu dạng chuỗi phân cách | để đơn giản, không cần jsonb extension.
    /// </summary>
    public string ExpectedTraitsPipe { get; set; } = string.Empty;

    /// <summary>
    /// Từ khóa keyword để AI / rule so khớp (vd: "robusta|buon ma thuot|buôn ma thuột|đắk lắk|dak lak").
    /// </summary>
    public string KeywordsPipe { get; set; } = string.Empty;

    /// <summary>Mô tả ngắn hiển thị cho buyer trên UI.</summary>
    public string? DisplayNote { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<ProductLocalMeta> ProductLocalMetas { get; set; } = new List<ProductLocalMeta>();
}
