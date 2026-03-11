using System;

namespace ECommerceAPI.Domain.Entities;

public partial class AiChatMessage
{
    public long Id { get; set; }

    public Guid SessionId { get; set; }

    public string Role { get; set; } = null!;

    public string Content { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public virtual AiChatSession Session { get; set; } = null!;
}
