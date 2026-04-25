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
    private readonly TimeSpan _textTimeout;
    private readonly TimeSpan _imageTimeout;
    private readonly int _maxHttpAttempts;

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public GeminiClientService(IConfiguration config, ILogger<GeminiClientService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _apiKey = config["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini:ApiKey is missing");
        _modelName = string.IsNullOrWhiteSpace(config["Gemini:Model"])
            ? "gemini-2.0-flash"
            : config["Gemini:Model"]!.Trim();
        _textTimeout = TimeSpan.FromSeconds(config.GetValue("Gemini:TimeoutSeconds", 90));
        _imageTimeout = TimeSpan.FromSeconds(config.GetValue("Gemini:ImageTimeoutSeconds", 150));
        _maxHttpAttempts = Math.Clamp(config.GetValue("Gemini:MaxHttpAttempts", 3), 1, 6);
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

        return await CallApiAsync(body, _textTimeout);
    }

    /// <summary>
    /// Gọi Gemini với JSON mode (text-only) — đảm bảo output là JSON hợp lệ theo schema.
    /// Schema dùng PascalCase C# properties; _jsonOpts sẽ serialize thành snake_case khi gửi lên API.
    /// </summary>
    public async Task<string> GenerateJsonAsync(string systemPrompt, string userMessage, object responseSchema)
    {
        var body = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = userMessage } } } },
            generation_config = new
            {
                response_mime_type = "application/json",
                response_schema = responseSchema
            }
        };

        return await CallApiAsync(body, _textTimeout);
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

        return await CallApiAsync(body, perCallTimeout: _textTimeout);
    }

    private async Task<string> CallApiAsync(object requestBody, TimeSpan perCallTimeout)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelName}:generateContent?key={_apiKey}";
        var json = JsonSerializer.Serialize(requestBody, _jsonOpts);

        // Backoff trước mỗi lần gọi lại 503/timeout
        int[] delayBeforeNextAttemptMs = [0, 2000, 4000];

        for (int attempt = 0; attempt < _maxHttpAttempts; attempt++)
        {
            if (attempt > 0)
            {
                var d = delayBeforeNextAttemptMs[Math.Min(attempt, delayBeforeNextAttemptMs.Length - 1)];
                if (d > 0) await Task.Delay(d);
            }

            using var cts = new CancellationTokenSource(perCallTimeout);
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

                // 503: thử lại nếu còn lượt
                if (statusCode == 503 && attempt < _maxHttpAttempts - 1)
                    continue;

                _logger.LogDebug("Gemini lỗi HTTP {Status}: {Body}", statusCode, responseBody);
                if (statusCode == 429)
                    return "⚠️ Đã vượt giới hạn API Gemini (rate limit). Vui lòng thử lại sau 1 phút.";
                if (statusCode == 503)
                    return "⚠️ Gemini server đang quá tải. Vui lòng thử lại sau 30 giây.";

                return $"⚠️ Lỗi Gemini {statusCode}: {ExtractErrorMessage(responseBody)}";
            }
            catch (OperationCanceledException)
            {
                if (attempt < _maxHttpAttempts - 1)
                    continue;
                return "⚠️ AI không phản hồi sau nhiều lần thử. Vui lòng thử lại sau.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gemini API lỗi không xác định");
                return $"⚠️ Lỗi AI: {ex.Message}";
            }
        }

        return $"⚠️ Gemini không khả dụng sau {_maxHttpAttempts} lần thử.";
    }

    /// <summary>
    /// Gọi Gemini với ảnh (multimodal). Tải ảnh từ URL về, encode base64, gửi cùng text prompt.
    /// </summary>
    public async Task<string> GenerateWithImagesAsync(string systemPrompt, string userMessage, List<string> imageUrls)
        => await GenerateWithImagesInternalAsync(systemPrompt, userMessage, imageUrls, forceJsonResponse: false, responseSchema: null);

    /// <summary>
    /// Gọi Gemini multimodal và yêu cầu trả về JSON chuẩn theo schema.
    /// </summary>
    public async Task<string> GenerateWithImagesJsonAsync(string systemPrompt, string userMessage, List<string> imageUrls, object responseSchema)
        => await GenerateWithImagesInternalAsync(systemPrompt, userMessage, imageUrls, forceJsonResponse: true, responseSchema);

    private async Task<string> GenerateWithImagesInternalAsync(
        string systemPrompt,
        string userMessage,
        List<string> imageUrls,
        bool forceJsonResponse,
        object? responseSchema)
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
                _logger.LogDebug(ex, "Không tải được ảnh: {Url}", url);
            }
        }

        if (parts.Count == 0)
            return "⚠️ Không thể tải bất kỳ ảnh nào từ các URL đã cung cấp.";

        // Thêm text prompt sau ảnh
        parts.Add(new { text = userMessage });

        object body;
        if (forceJsonResponse)
        {
            body = new
            {
                system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = new[] { new { role = "user", parts } },
                generation_config = new
                {
                    response_mime_type = "application/json",
                    response_schema = responseSchema
                }
            };
        }
        else
        {
            body = new
            {
                system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = new[] { new { role = "user", parts } }
            };
        }

        return await CallApiAsync(body, _imageTimeout);
    }

    /// <summary>Tải ảnh từ URL và trả về (base64, mimeType).</summary>
    private async Task<(string Base64, string MimeType)> DownloadImageAsBase64Async(string url)
    {
        if (url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = url.IndexOf(',');
            if (commaIndex <= 0) throw new InvalidOperationException("Data URL khong hop le");

            var meta = url[..commaIndex];
            var data = url[(commaIndex + 1)..];

            var dataMime = "image/jpeg";
            var mimeStart = "data:";
            var mimeEnd = meta.IndexOf(';');
            if (meta.StartsWith(mimeStart, StringComparison.OrdinalIgnoreCase) && mimeEnd > mimeStart.Length)
            {
                dataMime = meta.Substring(mimeStart.Length, mimeEnd - mimeStart.Length);
            }

            if (!meta.Contains(";base64", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Data URL phai o dang base64");

            return (data, dataMime);
        }

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
