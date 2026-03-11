using System.Text.Json;

namespace ECommerceAI.Data.Entities;

public class AiTagSuggestion
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid SellerId { get; set; }
    public string? InputTitle { get; set; }
    public string? InputDescription { get; set; }
    public long? SuggestedCategoryId { get; set; }
    public JsonDocument SuggestedTags { get; set; } = JsonDocument.Parse("[]");
    public long? ChosenCategoryId { get; set; }
    public JsonDocument ChosenTags { get; set; } = JsonDocument.Parse("[]");
    public string Action { get; set; } = "accepted";
    public DateTime CreatedAt { get; set; }
}
