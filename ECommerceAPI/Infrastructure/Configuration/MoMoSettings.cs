namespace ECommerceAPI.Infrastructure.Configuration;

public class MoMoSettings
{
    public const string SectionName = "MoMo";

    public string PartnerCode { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string NotifyUrl { get; set; } = string.Empty;
    public string RequestType { get; set; } = "payWithMethod";
}
