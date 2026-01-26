using System;

namespace ECommerceAPI.Domain.Entities;

public partial class ProductMaterial
{
    public Guid ProductId { get; set; }
    
    public Guid MaterialId { get; set; }
    
    public virtual Product Product { get; set; } = null!;
    
    public virtual Material Material { get; set; } = null!;
}
