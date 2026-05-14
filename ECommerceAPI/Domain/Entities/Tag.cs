using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class Tag
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
}
