using System.Text.RegularExpressions;

namespace ECommerceAPI.Application;

/// <summary>
/// Chuẩn hóa số điện thoại VN: bắt đầu bằng 0, 10–11 chữ số.
/// </summary>
public static partial class PhoneVnHelper
{
    [GeneratedRegex(@"^0[0-9]{9,10}$")]
    private static partial Regex LocalVnPhoneRegex();

    public static string? NormalizeToLocal(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        var raw = phone.Trim();
        var d = DigitsOnly(raw);
        if (d.Length == 0) return raw;

        if (d.StartsWith("84", StringComparison.Ordinal) && d.Length >= 10)
        {
            var end = Math.Min(12, d.Length);
            var rest = d[2..end];
            if (rest.Length >= 9)
            {
                if (rest[0] == '0' && (rest.Length == 10 || rest.Length == 11))
                    return rest;
                return "0" + rest;
            }
        }

        if (d[0] == '0' && d.Length is 10 or 11)
            return d;

        if (d.Length == 9 && d[0] is >= '3' and <= '9')
            return "0" + d;

        return d[0] == '0' ? d : raw;
    }

    public static string? FormatForDisplay(string? phone)
    {
        var n = NormalizeToLocal(phone);
        if (string.IsNullOrEmpty(n)) return n;

        return n.Length switch
        {
            10 => $"{n[..4]} {n.Substring(4, 3)} {n[7..]}",
            11 => $"{n[..4]} {n.Substring(4, 3)} {n[7..]}",
            _ => n
        };
    }

    public static bool IsValidLocalVn(string? phone)
    {
        var n = NormalizeToLocal(phone);
        return !string.IsNullOrEmpty(n) && LocalVnPhoneRegex().IsMatch(n);
    }

    private static string DigitsOnly(string s)
    {
        return string.Concat(s.Where(char.IsDigit));
    }
}
