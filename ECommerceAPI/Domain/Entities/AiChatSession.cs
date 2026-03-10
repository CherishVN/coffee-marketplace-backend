using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class AiChatSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Status { get; set; } = "active";

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual ICollection<AiChatMessage> AiChatMessages { get; set; } = new List<AiChatMessage>();

    public virtual ICollection<AiGeneratedCart> AiGeneratedCarts { get; set; } = new List<AiGeneratedCart>();

    public virtual ICollection<AiProductRecommendation> AiProductRecommendations { get; set; } = new List<AiProductRecommendation>();
}
