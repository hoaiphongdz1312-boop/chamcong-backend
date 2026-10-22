namespace NguyenHoaiPhong_1681.Helpers;

public static class MaskHelper
{
    /// <summary>danhnguyen@gmail.com → da*******@gmail.com</summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "";
        var at = email.IndexOf('@');
        if (at <= 0) return new string('*', email.Length);

        var local = email[..at];
        var domain = email[at..];
        var keep = local.Length <= 2 ? 1 : 2;
        return local[..keep] + new string('*', Math.Max(local.Length - keep, 3)) + domain;
    }
}
