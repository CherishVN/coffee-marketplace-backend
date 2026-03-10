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

// ── Save Feedback ─────────────────────────────────────────────────────────────

public class SaveSuggestionFeedbackDto
{
    public Guid LogId { get; set; }
    public long? ChosenCategoryId { get; set; }
    public List<long>? ChosenTagIds { get; set; }
    public List<Guid>? ChosenMaterialIds { get; set; }
    public string Action { get; set; } = "accepted";  // accepted | rejected | modified
}
