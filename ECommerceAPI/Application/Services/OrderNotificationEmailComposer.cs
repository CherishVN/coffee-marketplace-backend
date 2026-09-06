using ECommerceAPI.Application;
using ECommerceAPI.Application.EmailTemplates;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECommerceAPI.Application.Services;

public class OrderNotificationEmailComposer : IOrderNotificationEmailComposer
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public OrderNotificationEmailComposer(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<(string Subject, string Html)?> TryComposeAsync(
        Guid orderId,
        OrderStatus oldStatus,
        OrderStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Shop)
            .Include(o => o.Customer)
            .Include(o => o.Shipments)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .ThenInclude(p => p.ProductImages)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Variant)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
            return null;

        var fe = (_configuration["FrontendUrl"] ?? "https://ecomviet.vercel.app").TrimEnd('/');
        var brand = _configuration["Smtp:FromName"] ?? "E-Commerce";
        // Trang đơn mua (FE): app/user/purchase
        var orderDetailUrl = $"{fe}/user/purchase";

        var customerName = string.IsNullOrWhiteSpace(order.Customer.FullName)
            ? "Khách hàng"
            : order.Customer.FullName!.Trim();

        var lines = new List<OrderStatusEmailHtml.LineItem>();
        var n = 1;
        foreach (var oi in order.OrderItems.OrderBy(x => x.CreatedAt))
        {
            var img = oi.Product.ProductImages
                .OrderBy(p => p.SortOrder)
                .FirstOrDefault()?.ImageUrl;
            img = NormalizeAssetUrl(img, fe);

            lines.Add(new OrderStatusEmailHtml.LineItem(
                n++,
                oi.ProductName,
                oi.Variant?.VariantName,
                oi.Quantity,
                oi.LineTotal,
                img));
        }

        var deliveryUtc = newStatus == OrderStatus.Delivered
            ? order.PrimaryShipment()?.ActualDeliveryDate?.UtcDateTime
            : null;

        var html = OrderStatusEmailHtml.Build(
            brand,
            customerName,
            order.OrderCode,
            order.Shop.Name,
            order.CreatedAt.Kind == DateTimeKind.Utc ? order.CreatedAt : DateTime.SpecifyKind(order.CreatedAt, DateTimeKind.Utc),
            deliveryUtc,
            newStatus,
            lines,
            order.Subtotal,
            order.ShippingFee,
            order.Total,
            orderDetailUrl,
            newStatus == OrderStatus.Delivered ? orderDetailUrl : null);

        var subject = OrderStatusEmailHtml.GetSubject(order.OrderCode, newStatus);
        return (subject, html);

        static string? NormalizeAssetUrl(string? url, string frontendBase)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return url;
            return $"{frontendBase.TrimEnd('/')}/{url.TrimStart('/')}";
        }
    }
}
