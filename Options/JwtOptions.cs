namespace NguyenHoaiPhong_1681.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "NguyenHoaiPhong_1681";
    public string Audience { get; set; } = "NguyenHoaiPhong_1681.App";
    /// <summary>Khóa ký HMAC-SHA256, tối thiểu 32 ký tự.</summary>
    public string Key { get; set; } = "";
    public int AccessMinutes { get; set; } = 30;
    public int RefreshDays { get; set; } = 7;
}
