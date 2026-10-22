using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace NguyenHoaiPhong_1681.Helpers;

public static partial class PhoneHelper
{
    // Di động VN: 0[35789]xxxxxxxx | +84[35789]xxxxxxxx | 84[35789]xxxxxxxx
    [GeneratedRegex(@"^(?:0|\+?84)([35789]\d{8})$")]
    private static partial Regex VnMobileRegex();

    /// <summary>Chuẩn hóa số di động VN về dạng 84xxxxxxxxx. Trả false nếu không hợp lệ.</summary>
    public static bool TryNormalize(string? input, [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var cleaned = Regex.Replace(input.Trim(), @"[\s.\-()]", "");
        var match = VnMobileRegex().Match(cleaned);
        if (!match.Success) return false;

        normalized = "84" + match.Groups[1].Value;
        return true;
    }

    public static bool IsValid(string? input) => TryNormalize(input, out _);

    /// <summary>84912345571 → 84 9** *** 571</summary>
    public static string Mask(string? phone)
    {
        if (string.IsNullOrEmpty(phone)) return "";
        if (phone.Length != 11 || !phone.StartsWith("84")) return new string('*', Math.Max(phone.Length - 3, 0)) + phone[^Math.Min(3, phone.Length)..];
        return $"84 {phone[2]}** *** {phone[^3..]}";
    }
}
