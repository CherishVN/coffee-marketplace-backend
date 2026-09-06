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

// ── Unified Product Analysis (text-only, single Gemini call) ─────────────────

public class AnalyzeProductRequestDto
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    /// <summary>Category ID đã chọn (tuỳ chọn) — dùng làm ngữ cảnh để cải thiện độ chính xác tag/material.</summary>
    public long? CategoryId { get; set; }
}

public class AnalyzeProductResponseDto
{
    public List<CategorySuggestionItem> Categories { get; set; } = new();
    public List<TagSuggestionItem> Tags { get; set; } = new();
    public List<MaterialSuggestionItem> Materials { get; set; } = new();
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Ghi nhận phiên gợi ý tag + chất liệu sau khi seller tạo sản phẩm (analyze-product / analyze-image).
/// </summary>
public class CommitProductAiTagSessionDto
{
    public Guid ProductId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public long? CategoryId { get; set; }
    public List<CommitAiSuggestedTagDto> SuggestedTags { get; set; } = new();
    public List<string> ChosenTagNames { get; set; } = new();
    public List<CommitAiSuggestedMaterialDto> SuggestedMaterials { get; set; } = new();
    public List<Guid> ChosenMaterialIds { get; set; } = new();
}

public class CommitAiSuggestedTagDto
{
    public string TagName { get; set; } = null!;
    public decimal ConfidenceScore { get; set; }
}

public class CommitAiSuggestedMaterialDto
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
    public List<CategorySuggestionItem> SuggestedCategories { get; set; } = new();
    public List<TagSuggestionItem> SuggestedTags { get; set; } = new();
    public List<MaterialSuggestionItem> SuggestedMaterials { get; set; } = new();
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

// ── Save Feedback ─────────────────────────────────────────────────────────────

public class SaveSuggestionFeedbackDto
{
    public Guid LogId { get; set; }
    public long? ChosenCategoryId { get; set; }
    /// <summary>Tên các tag seller đã chọn (khớp với format chosen_tags trong DB: ["tag1", "tag2"])</summary>
    public List<string>? ChosenTagNames { get; set; }
    public List<Guid>? ChosenMaterialIds { get; set; }
    public string Action { get; set; } = "accepted";  // accepted | rejected | modified
}

public class SaveMaterialFeedbackDto
{
    public Guid LogId { get; set; }
    /// <summary>Các materialId seller đã giữ lại sau khi chỉnh sửa gợi ý</summary>
    public List<Guid>? ChosenMaterialIds { get; set; }
    public string Action { get; set; } = "accepted";  // accepted | rejected | modified
}

// ── Tag Suggestion Log ────────────────────────────────────────────────────────

/// <summary>Item trong suggest_tags JSONB: {"tag": "vải cotton", "confidence": 0.95}</summary>
public class SuggestedTagJsonItem
{
    public string Tag { get; set; } = null!;
    public decimal Confidence { get; set; }
}

public class TagSuggestionLogItem
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string? InputTitle { get; set; }
    public long? SuggestedCategoryId { get; set; }
    /// <summary>Tên các tag AI đã gợi ý kèm confidence</summary>
    public List<SuggestedTagJsonItem> SuggestedTags { get; set; } = new();
    /// <summary>Tên các tag seller đã chọn cuối cùng</summary>
    public List<string> ChosenTags { get; set; } = new();
    public string Action { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }
}

public class TagSuggestionLogResponse
{
    public List<TagSuggestionLogItem> Items { get; set; } = new();
    public int Total { get; set; }
    public int Accepted { get; set; }
    public int Modified { get; set; }
    public int Rejected { get; set; }
}

// ── Material Suggestion Log ───────────────────────────────────────────────────

public class SuggestedMaterialJsonItem
{
    public Guid? MaterialId { get; set; }
    public string MaterialName { get; set; } = null!;
    public decimal Confidence { get; set; }
}

public class MaterialSuggestionLogItem
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public List<SuggestedMaterialJsonItem> SuggestedMaterials { get; set; } = new();
    public List<Guid> ChosenMaterialIds { get; set; } = new();
    public List<string> ChosenMaterialNames { get; set; } = new();
    public string Action { get; set; } = "accepted";
    public DateTime CreatedAt { get; set; }
}

public class MaterialSuggestionLogResponse
{
    public List<MaterialSuggestionLogItem> Items { get; set; } = new();
    public int Total { get; set; }
    public int Accepted { get; set; }
    public int Modified { get; set; }
    public int Rejected { get; set; }
}
