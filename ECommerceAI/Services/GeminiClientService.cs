using System.Text;
using System.Text.Json;

namespace ECommerceAI.Services;

/// <summary>
/// Gọi Gemini REST API trực tiếp bằng HttpClient — tránh retry ẩn của SDK.
/// </summary>
public class GeminiClientService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _modelName;
    private readonly ILogger<GeminiClientService> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public GeminiClientService(IConfiguration config, ILogger<GeminiClientService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _apiKey = config["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini:ApiKey is missing");
        _modelName = config["Gemini:Model"] ?? "gemini-2.0-flash";
        _http = httpClientFactory.CreateClient("GeminiClient");
    }

    /// <summary>Gọi Gemini với system prompt và user message đơn giản.</summary>
    public async Task<string> GenerateAsync(string systemPrompt, string userMessage)
    {
        var body = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = userMessage } } } }
        };

        return await CallApiAsync(body);
    }

    /// <summary>Gọi Gemini với lịch sử hội thoại nhiều lượt.</summary>
    public async Task<string> ChatAsync(string systemPrompt, List<(string Role, string Text)> history)
    {
        var contents = history.Select(h => new
        {
            role = h.Role == "assistant" ? "model" : h.Role,
            parts = new[] { new { text = h.Text } }
        }).ToArray();

        var body = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents
        };

        return await CallApiAsync(body);
    }

    private async Task<string> CallApiAsync(object requestBody)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelName}:generateContent?key={_apiKey}";
        var json = JsonSerializer.Serialize(requestBody, _jsonOpts);

        // Retry tối đa 3 lần cho lỗi 503 (server overload tạm thời)
        int[] retryDelaysMs = [2000, 4000, 8000];

        for (int attempt = 0; attempt <= retryDelaysMs.Length; attempt++)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(url, content, cts.Token);
                var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
                var statusCode = (int)response.StatusCode;

                if (response.IsSuccessStatusCode)
                {
                    var doc = JsonDocument.Parse(responseBody);
                    return doc.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString() ?? string.Empty;
                }

                // 503: retry nếu còn lượt
                if (statusCode == 503 && attempt < retryDelaysMs.Length)
                {
                    var delay = retryDelaysMs[attempt];
                    _logger.LogWarning("Gemini 503 (server overload), retry {Attempt}/3 sau {Delay}ms...", attempt + 1, delay);
                    await Task.Delay(delay);
                    continue;
                }

                // Các lỗi khác: trả về ngay
                _logger.LogWarning("Gemini API trả về lỗi {Status}: {Body}", statusCode, responseBody);
                if (statusCode == 429)
                    return "⚠️ Đã vượt giới hạn API Gemini (rate limit). Vui lòng thử lại sau 1 phút.";
                if (statusCode == 503)
                    return "⚠️ Gemini server đang quá tải. Vui lòng thử lại sau 30 giây.";

                return $"⚠️ Lỗi Gemini {statusCode}: {ExtractErrorMessage(responseBody)}";
            }
            catch (OperationCanceledException)
            {
                if (attempt < retryDelaysMs.Length)
                {
                    _logger.LogWarning("Gemini timeout, retry {Attempt}/3...", attempt + 1);
                    await Task.Delay(retryDelaysMs[attempt]);
                    continue;
                }
                _logger.LogWarning("Gemini API timeout sau 30 giây (đã thử 3 lần)");
                return "⚠️ AI không phản hồi sau nhiều lần thử. Vui lòng thử lại sau.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gemini API lỗi không xác định");
                return $"⚠️ Lỗi AI: {ex.Message}";
            }
        }

        return "⚠️ Gemini không khả dụng sau 3 lần thử.";
    }

    /// <summary>
    /// Gọi Gemini với ảnh (multimodal). Tải ảnh từ URL về, encode base64, gửi cùng text prompt.
    /// </summary>
    public async Task<string> GenerateWithImagesAsync(string systemPrompt, string userMessage, List<string> imageUrls)
    {
        var parts = new List<object>();

        // Tải và encode từng ảnh
        foreach (var url in imageUrls.Take(3))
        {
            try
            {
                var (base64Data, mimeType) = await DownloadImageAsBase64Async(url);
                parts.Add(new
                {
                    inline_data = new { mime_type = mimeType, data = base64Data }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể tải ảnh từ URL: {Url}", url);
            }
        }

        if (parts.Count == 0)
            return "⚠️ Không thể tải bất kỳ ảnh nào từ các URL đã cung cấp.";

        // Thêm text prompt sau ảnh
        parts.Add(new { text = userMessage });

        var body = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { role = "user", parts } }
        };

        return await CallApiAsync(body);
    }

    /// <summary>Tải ảnh từ URL và trả về (base64, mimeType).</summary>
    private async Task<(string Base64, string MimeType)> DownloadImageAsBase64Async(string url)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var bytes = await _http.GetByteArrayAsync(url, cts.Token);

        // Xác định MIME type từ URL hoặc mặc định jpeg
        var mime = url.ToLower() switch
        {
            var u when u.Contains(".png") => "image/png",
            var u when u.Contains(".webp") => "image/webp",
            var u when u.Contains(".gif") => "image/gif",
            _ => "image/jpeg"
        };

        return (Convert.ToBase64String(bytes), mime);
    }

    /// <summary>Liệt kê tất cả models khả dụng với API key hiện tại.</summary>
    public async Task<string> ListModelsAsync()
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={_apiKey}&pageSize=50";
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var response = await _http.GetAsync(url, cts.Token);
            var body = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode) return $"Lỗi {(int)response.StatusCode}: {body}";

            var doc = JsonDocument.Parse(body);
            var models = doc.RootElement.GetProperty("models")
                .EnumerateArray()
                .Where(m => m.TryGetProperty("supportedGenerationMethods", out var methods) &&
                            methods.EnumerateArray().Any(x => x.GetString() == "generateContent"))
                .Select(m => m.GetProperty("name").GetString())
                .ToList();

            return string.Join("\n", models!);
        }
        catch (Exception ex) { return $"Lỗi: {ex.Message}"; }
    }

    private static string ExtractErrorMessage(string body)
    {
        try
        {
            var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("error").GetProperty("message").GetString() ?? body;
        }
        catch { return body; }
    }
}
