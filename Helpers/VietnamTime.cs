namespace NguyenHoaiPhong_1681.Helpers;

/// <summary>Quy đổi giờ Việt Nam (UTC+7, không có giờ mùa hè).</summary>
public static class VietnamTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static DateTime Now => DateTime.UtcNow + Offset;

    public static DateOnly Today => DateOnly.FromDateTime(Now);

    public static DateTime ToVietnam(DateTime utc) => utc + Offset;

    /// <summary>Giờ VN (ngày + giờ) → UTC.</summary>
    public static DateTime ToUtc(DateOnly date, TimeOnly time) =>
        DateTime.SpecifyKind(date.ToDateTime(time) - Offset, DateTimeKind.Utc);

    /// <summary>Thời điểm 00:00 giờ VN của ngày đó, quy ra UTC.</summary>
    public static DateTime StartOfDayUtc(DateOnly date) => ToUtc(date, TimeOnly.MinValue);

    /// <summary>Chuẩn hóa DateTime nhận từ client về UTC (không có múi giờ → coi là UTC).</summary>
    public static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
