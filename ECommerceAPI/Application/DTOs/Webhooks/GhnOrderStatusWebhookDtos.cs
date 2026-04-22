using System.Text.Json.Serialization;

namespace ECommerceAPI.Application.DTOs.Webhooks;

/// <summary>
/// Body POST từ GHN (order status callback). Tham chiếu GHN: Callback order status.
/// </summary>
public class GhnOrderStatusPayload
{
    [JsonPropertyName("OrderCode")]
    public string? OrderCode { get; set; }

    [JsonPropertyName("ClientOrderCode")]
    public string? ClientOrderCode { get; set; }

    [JsonPropertyName("Status")]
    public string? Status { get; set; }

    [JsonPropertyName("Type")]
    public string? Type { get; set; }

    [JsonPropertyName("Time")]
    public DateTime? Time { get; set; }

    [JsonPropertyName("TotalFee")]
    public decimal? TotalFee { get; set; }

    [JsonPropertyName("ShopID")]
    public int? ShopID { get; set; }

    [JsonPropertyName("CODAmount")]
    public decimal? CODAmount { get; set; }

    [JsonPropertyName("Description")]
    public string? Description { get; set; }
}
