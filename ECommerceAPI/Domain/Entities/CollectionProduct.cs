using System;

namespace ECommerceAPI.Domain.Entities;

public partial class CollectionProduct
{
    public int CollectionId { get; set; }
    public Guid ProductId { get; set; }

    public int? SortOrder { get; set; }
    public DateTime? AddedAt { get; set; }

    public virtual Collection Collection { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}
