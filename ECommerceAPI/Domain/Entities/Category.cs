using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class Category
{
    public long Id { get; set; }

    public long? ParentId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public short Level { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<AiTagSuggestion> AiTagSuggestionChosenCategories { get; set; } = new List<AiTagSuggestion>();

    public virtual ICollection<AiTagSuggestion> AiTagSuggestionSuggestedCategories { get; set; } = new List<AiTagSuggestion>();

    public virtual ICollection<Category> InverseParent { get; set; } = new List<Category>();

    public virtual Category? Parent { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
