namespace ECommerceAI.Data.Entities.ReadOnly;

public class Order
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ShopId { get; set; }
    public short Status { get; set; }
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
