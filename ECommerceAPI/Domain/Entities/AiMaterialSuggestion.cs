using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class AiMaterialSuggestion
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public Guid SellerId { get; set; }

    public string SuggestedMaterials { get; set; } = null!;

    public List<Guid>? ChosenMaterialIds { get; set; }

    public string Action { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual User Seller { get; set; } = null!;
}
