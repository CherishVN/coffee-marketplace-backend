using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class Collection
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Description { get; set; }
    public string? ShortDesc { get; set; }

    public string? Image { get; set; }
    public string? ImageAlt { get; set; }

    public string? Type { get; set; } = "manual";

    public long? CategoryId { get; set; }
    public long? TagId { get; set; }

    public bool ShowOnHome { get; set; }
    public int? HomeSortOrder { get; set; }

    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }

    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual Category? Category { get; set; }
    public virtual Tag? Tag { get; set; }
    
    public virtual ICollection<CollectionProduct> CollectionProducts { get; set; } = new List<CollectionProduct>();
}
