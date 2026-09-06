namespace ECommerceAI.Data.Entities.ReadOnly;

public class Dispute
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ShopId { get; set; }
    public short Status { get; set; }
    public short Type { get; set; }
    public decimal RequestedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}
