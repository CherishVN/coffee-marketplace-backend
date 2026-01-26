using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class Conversation
{
    public Guid Id { get; set; }

    public Guid BuyerId { get; set; }

    public Guid SellerId { get; set; }

    public Guid ShopId { get; set; }

    public Guid? OrderId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User Buyer { get; set; } = null!;

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual Order? Order { get; set; }

    public virtual User Seller { get; set; } = null!;

    public virtual Shop Shop { get; set; } = null!;
}
