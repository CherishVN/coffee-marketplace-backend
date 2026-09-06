using ECommerceAPI.Domain.Enums;

namespace ECommerceAPI.Application;

/// <summary>Nhãn trạng thái đơn hàng hiển thị cho user (thông báo, toast, in-app).</summary>
public static class OrderStatusVnHelper
{
    public static string Vietnamese(OrderStatus status) => status switch
    {
        OrderStatus.PendingPayment => "Chờ thanh toán",
        OrderStatus.PendingConfirmation => "Chờ xác nhận",
        OrderStatus.Confirmed => "Đã xác nhận",
        OrderStatus.Processing => "Đang chuẩn bị hàng",
        OrderStatus.Shipping => "Đang giao hàng",
        OrderStatus.Delivered => "Đã giao hàng",
        OrderStatus.Completed => "Hoàn thành",
        OrderStatus.Cancelled => "Đã hủy",
        OrderStatus.Refunded => "Đã hoàn tiền",
        OrderStatus.Returning => "Đang trả hàng",
        OrderStatus.Returned => "Đã nhận hàng trả",
        _ => status.ToString()
    };
}
