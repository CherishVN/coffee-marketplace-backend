using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class AiGeneratedCart
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public Guid? CartId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual AiChatSession Session { get; set; } = null!;

    public virtual Cart? Cart { get; set; }

    public virtual ICollection<AiRecommendationItem> AiRecommendationItems { get; set; } = new List<AiRecommendationItem>();
}
