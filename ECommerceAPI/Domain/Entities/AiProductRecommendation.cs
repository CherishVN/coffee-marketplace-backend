using System;

namespace ECommerceAPI.Domain.Entities;

public partial class AiProductRecommendation
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public Guid ProductId { get; set; }

    public decimal? Score { get; set; }

    public string? Reason { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual AiChatSession Session { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
