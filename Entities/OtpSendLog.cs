using NguyenHoaiPhong_1681.Enums;

namespace NguyenHoaiPhong_1681.Entities;

public class OtpSendLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public OtpChannel Channel { get; set; }
    /// <summary>Đích đến đã che.</summary>
    public string Destination { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
