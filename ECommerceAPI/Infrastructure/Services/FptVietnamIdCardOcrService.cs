using System.Net.Http.Headers;
using System.Text.Json;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace ECommerceAPI.Infrastructure.Services;

public class FptVietnamIdCardOcrService : IFptVietnamIdCardOcrService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FptAiSettings _settings;
    private readonly ILogger<FptVietnamIdCardOcrService> _logger;

    public FptVietnamIdCardOcrService(
        IHttpClientFactory httpClientFactory,
        IOptions<FptAiSettings> options,
        ILogger<FptVietnamIdCardOcrService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<FptVietnamIdOcrResult> RecognizeAsync(
        Stream imageStream,
        string fileName,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return new FptVietnamIdOcrResult
            {
                ErrorCode = -1,
                ErrorMessage = "FPT.AI API key chưa được cấu hình (FptAi:ApiKey)."
            };
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_settings.TimeoutSeconds, 5, 120));

        var url = _settings.IdReaderUrl.Trim();
        if (!url.EndsWith("/"))
            url += "/";

        var mediaType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType!;

        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(imageStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        content.Add(fileContent, "image", string.IsNullOrEmpty(fileName) ? "id.jpg" : fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.TryAddWithoutValidation("api-key", _settings.ApiKey);
        request.Content = content;

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FPT.AI ID reader request failed");
            return new FptVietnamIdOcrResult { ErrorCode = -2, ErrorMessage = "Không thể kết nối dịch vụ đọc CCCD" };
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        FptVietnamIdOcrResult? parsed;
        try
        {
            var doc = JsonDocument.Parse(string.IsNullOrEmpty(body) ? "{}" : body);
            var root = doc.RootElement;
            var err = root.TryGetProperty("errorCode", out var ec) ? ec.GetInt32() : -3;
            var msg = root.TryGetProperty("errorMessage", out var em) && em.ValueKind == JsonValueKind.String
                ? (em.GetString() ?? "")
                : "";

            FptVietnamIdOcrData? data = null;
            if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array && dataEl.GetArrayLength() > 0)
            {
                var first = dataEl[0];
                data = MapData(first);
            }

            parsed = new FptVietnamIdOcrResult
            {
                ErrorCode = err,
                ErrorMessage = msg,
                Data = data
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FPT.AI response parse error");
            return new FptVietnamIdOcrResult
            {
                ErrorCode = -3,
                ErrorMessage = "Phản hồi từ dịch vụ đọc CCCD không hợp lệ"
            };
        }

        if (!response.IsSuccessStatusCode && parsed.ErrorCode == 0)
        {
            return new FptVietnamIdOcrResult
            {
                ErrorCode = (int)response.StatusCode,
                ErrorMessage = string.IsNullOrEmpty(parsed.ErrorMessage) ? response.ReasonPhrase ?? "HTTP lỗi" : parsed.ErrorMessage
            };
        }

        return parsed;
    }

    private static FptVietnamIdOcrData? MapData(JsonElement el)
    {
        FptVietnamAddressEntities? ent = null;
        if (el.TryGetProperty("address_entities", out var ae) && ae.ValueKind == JsonValueKind.Object)
        {
            ent = new FptVietnamAddressEntities
            {
                Province = GetStr(ae, "province"),
                District = GetStr(ae, "district"),
                Ward = GetStr(ae, "ward"),
                Street = GetStr(ae, "street")
            };
        }

        return new FptVietnamIdOcrData
        {
            Type = GetStr(el, "type"),
            TypeNew = GetStr(el, "type_new"),
            Id = N(GetStr(el, "id")),
            Name = N(GetStr(el, "name")),
            Dob = N(GetStr(el, "dob")),
            Sex = N(GetStr(el, "sex")),
            Nationality = N(GetStr(el, "nationality")),
            Home = N(GetStr(el, "home")),
            Address = N(GetStr(el, "address")),
            AddressEntities = ent,
            IssueDate = N(GetStr(el, "issue_date")),
            IssueLoc = N(GetStr(el, "issue_loc")),
            Doe = N(GetStr(el, "doe")),
            Religion = N(GetStr(el, "religion")),
            Ethnicity = N(GetStr(el, "ethnicity")),
            Features = N(GetStr(el, "features"))
        };
    }

    private static string? GetStr(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    private static string? N(string? s) =>
        string.IsNullOrWhiteSpace(s) || s.Equals("N/A", StringComparison.OrdinalIgnoreCase) ? null : s.Trim();
}
