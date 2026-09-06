using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace ECommerceAPI.Infrastructure.Payment;

public class VNPayLibrary
{
    private readonly SortedList<string, string> _requestData = new(StringComparer.InvariantCultureIgnoreCase);
    private readonly SortedList<string, string> _responseData = new(StringComparer.InvariantCultureIgnoreCase);

    public void AddRequestData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
            _requestData[key] = value;
    }

    public void AddResponseData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
            _responseData[key] = value;
    }

    public string GetResponseData(string key)
    {
        return _responseData.TryGetValue(key, out var value) ? value : string.Empty;
    }

    public string CreateRequestUrl(string baseUrl, string hashSecret)
    {
        var data = new StringBuilder();

        foreach (var kv in _requestData.Where(kv => !string.IsNullOrEmpty(kv.Value)))
        {
            data.Append(WebUtility.UrlEncode(kv.Key));
            data.Append('=');
            data.Append(WebUtility.UrlEncode(kv.Value));
            data.Append('&');
        }

        // Remove trailing '&'
        if (data.Length > 0)
            data.Length--;

        string queryString = data.ToString();
        string secureHash = HmacSHA512(hashSecret, queryString);

        return $"{baseUrl}?{queryString}&vnp_SecureHash={secureHash}";
    }

    public bool ValidateSignature(string inputHash, string hashSecret)
    {
        var data = new StringBuilder();

        foreach (var kv in _responseData
            .Where(kv => kv.Key != "vnp_SecureHash" && kv.Key != "vnp_SecureHashType" && !string.IsNullOrEmpty(kv.Value)))
        {
            data.Append(WebUtility.UrlEncode(kv.Key));
            data.Append('=');
            data.Append(WebUtility.UrlEncode(kv.Value));
            data.Append('&');
        }

        if (data.Length > 0)
            data.Length--;

        string myChecksum = HmacSHA512(hashSecret, data.ToString());
        return myChecksum.Equals(inputHash, StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// Validate VNPay return signature using the raw query string to avoid
    /// URL-encoding mismatches (e.g. <c>WebUtility.UrlEncode</c> encodes
    /// space as '+' while VNPay uses '%20').
    /// </summary>
    public static bool ValidateSignatureRaw(string rawQueryString, string hashSecret)
    {
        var pairs = rawQueryString.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries);

        string? inputHash = null;
        var sorted = new SortedList<string, string>(StringComparer.InvariantCultureIgnoreCase);

        foreach (var pair in pairs)
        {
            var eqIdx = pair.IndexOf('=');
            if (eqIdx < 0) continue;

            var key = pair[..eqIdx];
            var rawValue = pair[(eqIdx + 1)..];

            if (key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase))
            {
                inputHash = rawValue;
                continue;
            }
            if (key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrEmpty(rawValue))
                sorted[key] = rawValue;
        }

        if (string.IsNullOrEmpty(inputHash)) return false;

        var data = new StringBuilder();
        foreach (var kv in sorted)
        {
            if (data.Length > 0) data.Append('&');
            data.Append(kv.Key);
            data.Append('=');
            data.Append(kv.Value);
        }

        string myChecksum = HmacSHA512(hashSecret, data.ToString());
        return myChecksum.Equals(inputHash, StringComparison.InvariantCultureIgnoreCase);
    }

    public static string HmacSHA512(string key, string inputData)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] inputBytes = Encoding.UTF8.GetBytes(inputData);

        using var hmac = new HMACSHA512(keyBytes);
        byte[] hashValue = hmac.ComputeHash(inputBytes);

        return Convert.ToHexString(hashValue).ToLower();
    }
}
