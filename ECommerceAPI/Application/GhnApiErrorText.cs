using System.Text.Json;

namespace ECommerceAPI.Application;

/// <summary>Đọc nội dung lỗi từ body JSON chuẩn GHN (code, message, code_message, code_message_value).</summary>
public static class GhnApiErrorText
{
    public static string FromResponseBody(string? responseText, int httpStatusCode)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return $"GHN: HTTP {httpStatusCode}";

        try
        {
            using var doc = JsonDocument.Parse(responseText);
            return FromRootElement(doc.RootElement, httpStatusCode);
        }
        catch
        {
            var t = responseText.Trim();
            return t.Length > 400 ? t[..400] + "…" : t;
        }
    }

    public static string FromRootElement(JsonElement root, int httpStatusCode)
    {
        int? businessCode = null;
        if (root.TryGetProperty("code", out var codeEl) && codeEl.ValueKind == JsonValueKind.Number)
            businessCode = codeEl.GetInt32();

        var message = GetStringOrFirstArrayString(root, "message");
        var codeMessage = GetStringOrFirstArrayString(root, "code_message");
        var codeMessageValue = GetStringOrFirstArrayString(root, "code_message_value");

        var detail = FirstNonEmpty(codeMessageValue, codeMessage, message);
        if (string.IsNullOrEmpty(detail))
            detail = $"HTTP {httpStatusCode}";

        if (businessCode is { } bc && bc != 200)
            return $"[GHN #{bc}] {detail}";

        return !string.IsNullOrEmpty(message) && message != detail && !detail.Contains(message, StringComparison.Ordinal)
            ? $"{detail} — {message}"
            : detail;
    }

    private static string? GetStringOrFirstArrayString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var p))
            return null;
        if (p.ValueKind == JsonValueKind.String)
            return p.GetString();
        if (p.ValueKind == JsonValueKind.Array && p.GetArrayLength() > 0)
        {
            var first = p[0];
            if (first.ValueKind == JsonValueKind.String)
                return first.GetString();
        }
        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return null;
    }
}
