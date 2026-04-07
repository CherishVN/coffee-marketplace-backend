using System.Globalization;
using System.Net;
using System.Text;
using ECommerceAPI.Domain.Enums;

namespace ECommerceAPI.Application.EmailTemplates;

/// <summary>
/// Email HTML đơn hàng (phong cách thương mại điện tử: tiêu đề cam, bảng chi tiết, tổng tiền).
/// </summary>
public static class OrderStatusEmailHtml
{
    private const string PrimaryOrange = "#EE4D2D";

    public static string GetSubject(string orderCode, OrderStatus newStatus)
    {
        return newStatus switch
        {
            OrderStatus.Delivered => $"Đơn hàng #{orderCode} đã giao hàng thành công",
            OrderStatus.Completed => $"Đơn hàng #{orderCode} đã hoàn thành",
            OrderStatus.Shipping => $"Đơn hàng #{orderCode} đang được giao",
            OrderStatus.Cancelled => $"Đơn hàng #{orderCode} đã bị hủy",
            OrderStatus.Refunded => $"Đơn hàng #{orderCode} — hoàn tiền",
            _ => $"Cập nhật đơn hàng #{orderCode}",
        };
    }
    private const string BgDark = "#1a1a1a";
    private const string BgCard = "#2a2a2a";
    private const string TextMuted = "#a0a0a0";

    public sealed record LineItem(
        int Index,
        string ProductName,
        string? VariantName,
        int Quantity,
        decimal LineTotal,
        string? ImageUrl);

    public static string Build(
        string brandName,
        string customerName,
        string orderCode,
        string shopName,
        DateTime orderCreatedAtUtc,
        DateTime? deliveryDateUtc,
        OrderStatus newStatus,
        IReadOnlyList<LineItem> items,
        decimal subtotal,
        decimal shippingFee,
        decimal total,
        string? ordersPageUrl,
        string? confirmReceivedUrl)
    {
        var vn = CultureInfo.GetCultureInfo("vi-VN");
        var createdStr = orderCreatedAtUtc.ToString("dd/MM/yyyy HH:mm:ss", vn);
        var deliveryStr = (deliveryDateUtc ?? DateTime.UtcNow).ToString("dd/MM/yyyy", vn);

        var (headline, leadHtml) = newStatus switch
        {
            OrderStatus.Delivered => (
                $"Đơn hàng #{WebUtility.HtmlEncode(orderCode)} đã giao hàng thành công",
                $@"<p style=""margin:0 0 16px;color:#e8e8e8;line-height:1.6;font-size:15px"">
Xin chào <strong>{WebUtility.HtmlEncode(customerName)}</strong>,</p>
<p style=""margin:0 0 16px;color:#e8e8e8;line-height:1.6;font-size:15px"">
Đơn hàng <strong style=""color:{PrimaryOrange}"">#{WebUtility.HtmlEncode(orderCode)}</strong> của bạn đã được giao thành công ngày <strong>{WebUtility.HtmlEncode(deliveryStr)}</strong>.
</p>
<p style=""margin:0 0 20px;color:{TextMuted};line-height:1.6;font-size:14px"">
Vui lòng đăng nhập để xác nhận đã nhận hàng và hài lòng trong vòng 3 ngày. Sau khi xác nhận, thanh toán sẽ được chuyển cho người bán <strong>{WebUtility.HtmlEncode(shopName)}</strong>.
</p>"),
            OrderStatus.Completed => (
                $"Đơn hàng #{WebUtility.HtmlEncode(orderCode)} đã hoàn thành",
                $@"<p style=""margin:0 0 16px;color:#e8e8e8;line-height:1.6;font-size:15px"">
Xin chào <strong>{WebUtility.HtmlEncode(customerName)}</strong>,</p>
<p style=""margin:0 0 20px;color:#e8e8e8;line-height:1.6;font-size:15px"">
Cảm ơn bạn đã xác nhận. Đơn hàng <strong style=""color:{PrimaryOrange}"">#{WebUtility.HtmlEncode(orderCode)}</strong> đã hoàn thành.
</p>"),
            _ => (
                $"Cập nhật đơn hàng #{WebUtility.HtmlEncode(orderCode)}",
                $@"<p style=""margin:0 0 16px;color:#e8e8e8;line-height:1.6;font-size:15px"">
Xin chào <strong>{WebUtility.HtmlEncode(customerName)}</strong>,</p>
<p style=""margin:0 0 20px;color:#e8e8e8;line-height:1.6;font-size:15px"">
Trạng thái đơn hàng <strong style=""color:{PrimaryOrange}"">#{WebUtility.HtmlEncode(orderCode)}</strong> vừa được cập nhật: <strong>{WebUtility.HtmlEncode(newStatus.ToString())}</strong>.
</p>")
        };

        var discount = subtotal + shippingFee - total;
        if (discount < 0) discount = 0;

        var sbItems = new StringBuilder();
        foreach (var it in items)
        {
            var variant = string.IsNullOrWhiteSpace(it.VariantName) ? "—" : it.VariantName;
            var img = string.IsNullOrWhiteSpace(it.ImageUrl)
                ? ""
                : $@"<img src=""{WebUtility.HtmlEncode(it.ImageUrl)}"" width=""72"" height=""72"" style=""border-radius:8px;object-fit:cover;vertical-align:top"" alt="""" />";

            sbItems.Append($@"
<tr>
  <td style=""padding:12px 8px;border-bottom:1px solid #3a3a3a;vertical-align:top;width:80px"">{img}</td>
  <td style=""padding:12px 8px;border-bottom:1px solid #3a3a3a;vertical-align:top"">
    <div style=""font-weight:600;color:#f0f0f0;font-size:14px"">{it.Index}. {WebUtility.HtmlEncode(it.ProductName)}</div>
    <div style=""font-size:13px;color:{TextMuted};margin-top:4px"">Màu / mã: {WebUtility.HtmlEncode(variant)}</div>
    <div style=""font-size:13px;color:{TextMuted};margin-top:4px"">Số lượng: {it.Quantity}</div>
  </td>
  <td style=""padding:12px 8px;border-bottom:1px solid #3a3a3a;text-align:right;white-space:nowrap;color:#f0f0f0;font-size:14px"">{FormatVnd(it.LineTotal, vn)}</td>
</tr>");
        }

        var ctaBlock = "";
        if (!string.IsNullOrWhiteSpace(confirmReceivedUrl) && newStatus == OrderStatus.Delivered)
        {
            ctaBlock = $@"
<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin:24px auto"">
  <tr>
    <td style=""border-radius:8px;background:{PrimaryOrange};text-align:center"">
      <a href=""{WebUtility.HtmlEncode(confirmReceivedUrl)}"" style=""display:inline-block;padding:14px 32px;color:#ffffff;text-decoration:none;font-weight:700;font-size:15px"">Đã nhận hàng</a>
    </td>
  </tr>
</table>";
        }

        var viewOrderCta = "";
        if (!string.IsNullOrWhiteSpace(ordersPageUrl))
        {
            viewOrderCta = $@"
<p style=""text-align:center;margin:8px 0 0"">
  <a href=""{WebUtility.HtmlEncode(ordersPageUrl)}"" style=""color:{PrimaryOrange};font-size:14px"">Xem chi tiết đơn hàng</a>
</p>";
        }

        return $@"<!DOCTYPE html>
<html>
<head><meta charset=""utf-8"" /><meta name=""viewport"" content=""width=device-width"" /></head>
<body style=""margin:0;padding:0;background:{BgDark};font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:{BgDark};padding:24px 12px"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" style=""max-width:600px;background:{BgCard};border-radius:12px;overflow:hidden"">
          <tr>
            <td style=""padding:24px 20px 8px;text-align:center"">
              <div style=""font-size:22px;font-weight:800;color:{PrimaryOrange};letter-spacing:-0.5px"">{WebUtility.HtmlEncode(brandName)}</div>
            </td>
          </tr>
          <tr>
            <td style=""padding:8px 24px 4px"">
              <h1 style=""margin:0;font-size:18px;color:#ffffff;font-weight:700;text-align:center;line-height:1.4"">{headline}</h1>
            </td>
          </tr>
          <tr>
            <td style=""padding:16px 24px 8px"">
              {leadHtml}
              {ctaBlock}
            </td>
          </tr>
          <tr>
            <td style=""padding:8px 24px 4px"">
              <div style=""font-size:13px;font-weight:700;color:{TextMuted};text-transform:uppercase;letter-spacing:0.5px"">Thông tin đơn hàng (người mua)</div>
            </td>
          </tr>
          <tr>
            <td style=""padding:8px 24px 16px"">
              <table role=""presentation"" width=""100%"" style=""font-size:14px;color:#e8e8e8"">
                <tr><td style=""padding:6px 0;color:{TextMuted}"">Mã đơn hàng</td><td style=""padding:6px 0;text-align:right;color:{PrimaryOrange};font-weight:700"">#{WebUtility.HtmlEncode(orderCode)}</td></tr>
                <tr><td style=""padding:6px 0;color:{TextMuted}"">Ngày đặt</td><td style=""padding:6px 0;text-align:right"">{WebUtility.HtmlEncode(createdStr)}</td></tr>
                <tr><td style=""padding:6px 0;color:{TextMuted}"">Người bán</td><td style=""padding:6px 0;text-align:right"">{WebUtility.HtmlEncode(shopName)}</td></tr>
              </table>
            </td>
          </tr>
          <tr>
            <td style=""padding:0 16px"">
              <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
                {sbItems}
              </table>
            </td>
          </tr>
          <tr>
            <td style=""padding:16px 24px 24px"">
              <table role=""presentation"" width=""100%"" style=""font-size:14px;color:#e8e8e8"">
                <tr><td style=""padding:6px 0"">Tổng tiền hàng</td><td style=""padding:6px 0;text-align:right"">{FormatVnd(subtotal, vn)}</td></tr>
                {(discount > 0.01m ? $@"<tr><td style=""padding:6px 0;color:{TextMuted}"">Giảm giá / khuyến mãi</td><td style=""padding:6px 0;text-align:right;color:#4cd964"">-{FormatVnd(discount, vn)}</td></tr>" : "")}
                <tr><td style=""padding:6px 0;color:{TextMuted}"">Phí vận chuyển</td><td style=""padding:6px 0;text-align:right"">{FormatVnd(shippingFee, vn)}</td></tr>
                <tr><td colspan=""2"" style=""border-top:1px solid #3a3a3a;padding-top:12px;margin-top:8px""></td></tr>
                <tr><td style=""padding:6px 0;font-weight:700;font-size:16px"">Tổng thanh toán</td><td style=""padding:6px 0;text-align:right;font-weight:700;font-size:16px;color:{PrimaryOrange}"">{FormatVnd(total, vn)}</td></tr>
              </table>
              {viewOrderCta}
              <p style=""margin:24px 0 0;font-size:12px;color:{TextMuted};text-align:center"">
                Đây là email tự động từ {WebUtility.HtmlEncode(brandName)}. Vui lòng không trả lời trực tiếp email này.
              </p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
    }

    private static string FormatVnd(decimal v, CultureInfo vn) =>
        "₫" + v.ToString("N0", vn);
}
