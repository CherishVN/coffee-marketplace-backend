namespace ECommerceAI.Middleware;

/// <summary>
/// Cho phép Main API gọi AI service bằng internal API key (không cần JWT).
/// Inject user identity giả khi dùng internal key.
/// </summary>
public class InternalApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _internalApiKey;

    public InternalApiKeyMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _internalApiKey = config["InternalAuth:ApiKey"] ?? "ai-internal-secret-key-2026";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Internal-Key", out var key) && key == _internalApiKey)
        {
            // Đánh dấu request là internal - bypass JWT auth
            context.Items["IsInternalRequest"] = true;
        }

        await _next(context);
    }
}
