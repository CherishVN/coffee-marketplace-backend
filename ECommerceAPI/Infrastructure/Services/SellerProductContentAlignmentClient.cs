using System.Text;
using System.Text.Json;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace ECommerceAPI.Infrastructure.Services;

/// <summary>
/// Dùng cùng API phân tích sản phẩm của seller (analyze-image / analyze-product trên ECommerceAI).
/// Coi là khớp nếu <paramref name="expectedCategoryId"/> nằm trong top gợi ý category của AI.
/// </summary>
public class SellerProductContentAlignmentClient : ISellerProductContentAlignmentClient
{
    private const string PathAnalyzeImage = "/api/ai/seller/analyze-image";
    private const string PathAnalyzeProduct = "/api/ai/seller/analyze-product";

    private readonly HttpClient _http;
    private readonly AiServiceSettings _settings;

    private static readonly JsonSerializerOptions JsonWrite = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public SellerProductContentAlignmentClient(
        HttpClient httpClient,
        IOptions<AiServiceSettings> settings)
    {
        _http = httpClient;
        _settings = settings.Value;

        if (!string.IsNullOrWhiteSpace(_settings.BaseUrl))
            _http.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");

        var sec = Math.Max(_settings.TimeoutSeconds, 90);
        _http.Timeout = TimeSpan.FromSeconds(sec);

        if (!string.IsNullOrEmpty(_settings.ApiKey))
            _http.DefaultRequestHeaders.Add("X-API-Key", _settings.ApiKey);
    }

    public async Task<(bool Ok, string? ErrorMessage)> ValidateContentMatchesCategoryAsync(
        string? authorizationHeader,
        string title,
        string? description,
        IReadOnlyList<string> imageUrls,
        long expectedCategoryId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
            return (true, null);

        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return (false, "Thiếu thông tin xác thực. Vui lòng đăng nhập lại.");

        var hasImages = imageUrls is { Count: > 0 };
        string path;
        string json;
        if (hasImages)
        {
            var urls = imageUrls!.Take(2).ToList();
            path = PathAnalyzeImage;
            json = JsonSerializer.Serialize(
                new
                {
                    imageUrls = urls,
                    productTitle = title,
                    productDescription = description,
                },
                JsonWrite);
        }
        else
        {
            path = PathAnalyzeProduct;
            json = JsonSerializer.Serialize(
                new
                {
                    title,
                    description,
                    categoryId = (long?)null,
                },
                JsonWrite);
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, path);
        req.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var resp = await _http.SendAsync(req, cancellationToken);
        var text = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
            return MapHttpError(resp.StatusCode, text);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(text);
        }
        catch
        {
            return (false, "Phản hồi từ dịch vụ AI không đọc được. Vui lòng thử lại.");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.TryGetProperty("success", out var succ) && succ.ValueKind == JsonValueKind.False)
            {
                var err = TryPickString(root, "errorMessage", "ErrorMessage", "message", "Message");
                return (false, err ?? "Phân tích AI thất bại. Vui lòng thử lại.");
            }

            var arrayName = hasImages ? "suggestedCategories" : "categories";
            if (!root.TryGetProperty(arrayName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return (false, "AI không trả gợi ý danh mục. Thử lại sau.");

            var topIds = new List<long>(3);
            foreach (var item in arr.EnumerateArray())
            {
                if (item.TryGetProperty("categoryId", out var idEl) && idEl.TryGetInt64(out var id))
                    topIds.Add(id);
            }

            if (topIds.Count == 0)
                return (false, "AI không gợi ý được danh mục phù hợp. Kiểm tra tên/mô tả/ảnh rồi thử lại.");

            if (!topIds.Take(3).Contains(expectedCategoryId))
            {
                return (false,
                    "Nội dung tên, mô tả hoặc ảnh không khớp mặt hàng (danh mục) sản phẩm. Hãy chỉnh lại cho đúng loại, hoặc tạo sản phẩm mới.");
            }
        }

        return (true, null);
    }

    private static string? TryPickString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    return s.Trim();
            }
        }
        return null;
    }

    private static (bool, string?) MapHttpError(System.Net.HttpStatusCode status, string text)
    {
        string? detail = null;
        if (!string.IsNullOrWhiteSpace(text))
        {
            try
            {
                using var errDoc = JsonDocument.Parse(text);
                detail = TryPickString(
                    errDoc.RootElement,
                    "message", "Message", "title", "Title", "detail", "Detail");
            }
            catch
            {
                // bỏ qua
            }
        }

        if (!string.IsNullOrWhiteSpace(detail))
            return (false, detail);

        var st = (int)status;
        if (st == 401)
            return (false, "Phiên đăng nhập không hợp lệ. Hãy đăng nhập lại.");
        if (st == 404)
            return (false, "Không tìm thấy API phân tích trên dịch vụ AI. Kiểm tra AiService:BaseUrl tới ECommerceAI.");
        if (st >= 500)
            return (false, "Dịch vụ AI tạm thời lỗi. Thử lại sau.");

        return (false, "Không gọi được dịch vụ AI (HTTP " + st + ").");
    }
}
