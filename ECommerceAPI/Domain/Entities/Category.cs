using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceAPI.Domain.Entities;

public partial class Category
{
    public long Id { get; set; }

    public long? ParentId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Image { get; set; }

    public short Level { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }



    public virtual ICollection<Category> InverseParent { get; set; } = new List<Category>();

    public virtual Category? Parent { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
