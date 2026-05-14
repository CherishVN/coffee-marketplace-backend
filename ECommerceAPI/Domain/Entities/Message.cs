using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class Message
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public Guid SenderId { get; set; }

    public string MessageType { get; set; } = null!;

    public string Content { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual User Sender { get; set; } = null!;
}
