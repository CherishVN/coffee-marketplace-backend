namespace ECommerceAPI.Application.DTOs.Seller;

public class SellerProductReviewItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductThumbnailUrl { get; set; }
    public string? BuyerName { get; set; }
    public short Rating { get; set; }
    public string? Comment { get; set; }
    public string? SellerReply { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> ImageUrls { get; set; } = new();
}

public class ReplyToReviewDto
{
    public string Reply { get; set; } = string.Empty;
}

/// <summary>Đánh giá sản phẩm của khách (theo shop), có phân trang và thống kê.</summary>
public class SellerProductReviewsDataDto
{
    public List<SellerProductReviewItemDto> Reviews { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public double AverageRating { get; set; }
    /// <summary>Số lượng theo sao: [5★, 4★, 3★, 2★, 1★]</summary>
    public int[] RatingDistribution { get; set; } = new int[5];
    /// <summary>Chưa có phản hồi từ seller trên DB — tạm = TotalCount.</summary>
    public int PendingReplyCount { get; set; }
}
