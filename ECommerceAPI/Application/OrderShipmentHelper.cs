using ECommerceAPI.Domain.Entities;

namespace ECommerceAPI.Application;

/// <summary>Đọc thông tin vận chuyển từ bảng <c>shipments</c> (không còn trùng cột trên <c>orders</c>).</summary>
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

    /// <summary>
    /// Mã tạm lúc checkout (PEND-*); không phải mã vận đơn từ GHN.
    /// </summary>
    public static bool IsPendingPlaceholderTracking(string? trackingCode)
    {
        var t = trackingCode?.Trim();
        if (string.IsNullOrEmpty(t)) return true;
        return t.StartsWith("PEND-", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Một bản ghi dùng cho API/ UI: ưu tiên bản đã có mã thật từ GHN (bỏ qua bản tạm PEND-*).
    /// Có thể là bản tạo sau (webhook) trong khi bản cũ vẫn là PEND.
    /// </summary>
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

    /// <summary>Chỉ trả mã vận đơn nếu đã là mã từ GHN (ẩn PEND-*)</summary>
    public static string? GhnDisplayTrackingOrNull(this Shipment? s)
    {
        if (s == null) return null;
        var t = s.TrackingCode?.Trim();
        if (string.IsNullOrEmpty(t) || IsPendingPlaceholderTracking(t)) return null;
        return t;
    }

    /// <summary>
    /// Mã GHN thật trên bất kỳ bản ghi shipment nào (dùng khi cần cancel API, v.v.).
    /// </summary>
    public static string? GhnTrackingOrNull(IEnumerable<Shipment>? shipments)
    {
        return GhnDisplayTrackingOrNull(ShipmentForDisplay(shipments));
    }
}
