using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class Role
{
    public short Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
