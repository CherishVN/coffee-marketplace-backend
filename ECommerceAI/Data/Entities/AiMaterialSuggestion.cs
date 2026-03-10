using System.Text.Json;

namespace ECommerceAI.Data.Entities;

public class AiMaterialSuggestion
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid SellerId { get; set; }
    public JsonDocument SuggestedMaterials { get; set; } = JsonDocument.Parse("[]");
    public Guid[]? ChosenMaterialIds { get; set; }
    public string Action { get; set; } = "accepted";
    public DateTime CreatedAt { get; set; }
}
