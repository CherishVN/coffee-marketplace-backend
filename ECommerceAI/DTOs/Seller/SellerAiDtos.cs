namespace ECommerceAI.DTOs.Seller;

// ── Category Suggestion ───────────────────────────────────────────────────────

public class SuggestCategoryRequestDto
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public List<string>? ImageUrls { get; set; }
}

public class SuggestCategoryResponseDto
{
    public List<CategorySuggestionItem> Suggestions { get; set; } = new();
    public Guid? LogId { get; set; }
}

public class CategorySuggestionItem
{
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public string CategoryPath { get; set; } = null!;   // e.g. "Thời trang > Nam > Áo sơ mi"
    public decimal ConfidenceScore { get; set; }
}

// ── Tag Suggestion ────────────────────────────────────────────────────────────

public class SuggestTagsRequestDto
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public long? CategoryId { get; set; }
    public Guid? ProductId { get; set; }
}

public class SuggestTagsResponseDto
{
    public List<TagSuggestionItem> Suggestions { get; set; } = new();
    public Guid? LogId { get; set; }
}

public class TagSuggestionItem
{
    public long? TagId { get; set; }
    public string TagName { get; set; } = null!;
    public decimal ConfidenceScore { get; set; }
}

// ── Material Suggestion ───────────────────────────────────────────────────────

public class SuggestMaterialsRequestDto
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public long? CategoryId { get; set; }
    public Guid? ProductId { get; set; }
}

public class SuggestMaterialsResponseDto
{
    public List<MaterialSuggestionItem> Suggestions { get; set; } = new();
    public Guid? LogId { get; set; }
}

public class MaterialSuggestionItem
{
    public Guid? MaterialId { get; set; }
    public string MaterialName { get; set; } = null!;
    public decimal ConfidenceScore { get; set; }
}

// ── Image Analysis ────────────────────────────────────────────────────────────

public class AnalyzeImageRequestDto
{
    /// <summary>Danh sách URL ảnh sản phẩm (tối đa 3 ảnh)</summary>
    public List<string> ImageUrls { get; set; } = new();

    /// <summary>Tên sản phẩm (tuỳ chọn, giúp AI phân tích chính xác hơn)</summary>
    public string? ProductTitle { get; set; }

    /// <summary>Mô tả sản phẩm (tuỳ chọn)</summary>
    public string? ProductDescription { get; set; }
}

public class AnalyzeImageResponseDto
{
    public ImageQualityDto Quality { get; set; } = new();
    public List<CategorySuggestionItem> SuggestedCategories { get; set; } = new();
    public List<TagSuggestionItem> SuggestedTags { get; set; } = new();
    public List<MaterialSuggestionItem> SuggestedMaterials { get; set; } = new();
    public List<string> Improvements { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ImageQualityDto
{
    /// <summary>Điểm chất lượng ảnh từ 1-10</summary>
    public int Score { get; set; }

    /// <summary>Đánh giá: excellent | good | fair | poor</summary>
    public string Rating { get; set; } = string.Empty;

    public bool HasGoodLighting { get; set; }
    public bool HasCleanBackground { get; set; }
    public bool IsProductCentered { get; set; }
    public bool HasHighResolution { get; set; }
}

// ── Save Feedback ─────────────────────────────────────────────────────────────

public class SaveSuggestionFeedbackDto
{
    public Guid LogId { get; set; }
    public long? ChosenCategoryId { get; set; }
    public List<long>? ChosenTagIds { get; set; }
    public List<Guid>? ChosenMaterialIds { get; set; }
    public string Action { get; set; } = "accepted";  // accepted | rejected | modified
}
