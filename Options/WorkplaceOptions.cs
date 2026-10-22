namespace NguyenHoaiPhong_1681.Options;

public class WorkplaceOptions
{
    public const string SectionName = "Workplace";

    /// <summary>Vĩ độ công ty (0 = chưa cấu hình).</summary>
    public double Latitude { get; set; }
    /// <summary>Kinh độ công ty (0 = chưa cấu hình).</summary>
    public double Longitude { get; set; }
    /// <summary>Bán kính cho phép chấm công (mét).</summary>
    public int RadiusMeters { get; set; } = 100;
}
