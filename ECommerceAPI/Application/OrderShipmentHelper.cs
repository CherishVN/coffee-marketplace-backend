using ECommerceAPI.Domain.Entities;

namespace ECommerceAPI.Application;

public static class OrderShipmentHelper
{
    public static Shipment? PrimaryShipment(this Order order)
    {
        return order.Shipments?.OrderBy(s => s.CreatedAt).FirstOrDefault();
    }

    public static Shipment? PrimaryShipment(IEnumerable<Shipment>? shipments)
    {
        return shipments?.OrderBy(s => s.CreatedAt).FirstOrDefault();
    }

    public static bool IsPendingPlaceholderTracking(string? trackingCode)
    {
        var t = trackingCode?.Trim();
        if (string.IsNullOrEmpty(t)) return true;
        return t.StartsWith("PEND-", StringComparison.OrdinalIgnoreCase);
    }

    public static Shipment? ShipmentForDisplay(this Order order) =>
        ShipmentForDisplay(order.Shipments);

    public static Shipment? ShipmentForDisplay(IEnumerable<Shipment>? shipments)
    {
        if (shipments == null) return null;
        var list = shipments as IReadOnlyList<Shipment> ?? shipments.ToList();
        if (list.Count == 0) return null;

        var withRealGhn = list
            .Where(s => !IsPendingPlaceholderTracking(s.TrackingCode))
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefault();
        if (withRealGhn != null) return withRealGhn;
        return list.OrderBy(s => s.CreatedAt).FirstOrDefault();
    }

    public static string? GhnDisplayTrackingOrNull(this Shipment? s)
    {
        if (s == null) return null;
        var t = s.TrackingCode?.Trim();
        if (string.IsNullOrEmpty(t) || IsPendingPlaceholderTracking(t)) return null;
        return t;
    }

    public static string? GhnTrackingOrNull(IEnumerable<Shipment>? shipments)
    {
        return GhnDisplayTrackingOrNull(ShipmentForDisplay(shipments));
    }
}
