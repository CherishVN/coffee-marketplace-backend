namespace ECommerceAPI.Application.Interfaces;

/// <summary>
/// Gọi FPT.AI Vietnam ID Card Recognition — trích xuất thông tin từ ảnh CCCD/CMND.
/// </summary>
public interface IFptVietnamIdCardOcrService
{
    /// <param name="imageStream">Nội dung file ảnh</param>
    /// <param name="fileName">Tên file (để gửi multipart)</param>
    /// <param name="contentType">image/jpeg, image/png, ...</param>
    Task<FptVietnamIdOcrResult> RecognizeAsync(Stream imageStream, string fileName, string? contentType, CancellationToken cancellationToken = default);
}

public sealed class FptVietnamIdOcrResult
{
    public int ErrorCode { get; init; }
    public string ErrorMessage { get; init; } = "";
    public FptVietnamIdOcrData? Data { get; init; }
}

public sealed class FptVietnamIdOcrData
{
    public string? Type { get; init; }
    public string? TypeNew { get; init; }
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Dob { get; init; }
    public string? Sex { get; init; }
    public string? Nationality { get; init; }
    public string? Home { get; init; }
    public string? Address { get; init; }
    public FptVietnamAddressEntities? AddressEntities { get; init; }
    public string? IssueDate { get; init; }
    public string? IssueLoc { get; init; }
    public string? Doe { get; init; }
    /// <summary>Mặt sau CMND/CCCD</summary>
    public string? Religion { get; init; }
    public string? Ethnicity { get; init; }
    public string? Features { get; init; }
}

public sealed class FptVietnamAddressEntities
{
    public string? Province { get; init; }
    public string? District { get; init; }
    public string? Ward { get; init; }
    public string? Street { get; init; }
}
