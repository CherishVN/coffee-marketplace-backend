using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class AiTagSuggestion
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public Guid SellerId { get; set; }

    public string? InputTitle { get; set; }

    public string? InputDescription { get; set; }

    public long? SuggestedCategoryId { get; set; }

    public string SuggestedTags { get; set; } = null!;

    public long? ChosenCategoryId { get; set; }

    public string ChosenTags { get; set; } = null!;

    public string Action { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Category? ChosenCategory { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual User Seller { get; set; } = null!;

    public virtual Category? SuggestedCategory { get; set; }
}
