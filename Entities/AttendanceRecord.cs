using NguyenHoaiPhong_1681.Enums;

namespace NguyenHoaiPhong_1681.Entities;

public class AttendanceRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;
    /// <summary>Ngày làm việc theo giờ Việt Nam.</summary>
    public DateOnly WorkDate { get; set; }
    public DateTime CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public AttendanceStatus Status { get; set; }
    public int WorkMinutes { get; set; }
    public string? Note { get; set; }
    /// <summary>Vị trí GPS và khoảng cách (m) tới công ty lúc check-in.</summary>
    public double? CheckInLatitude { get; set; }
    public double? CheckInLongitude { get; set; }
    public int? CheckInDistanceMeters { get; set; }
    /// <summary>Vị trí GPS và khoảng cách (m) tới công ty lúc check-out.</summary>
    public double? CheckOutLatitude { get; set; }
    public double? CheckOutLongitude { get; set; }
    public int? CheckOutDistanceMeters { get; set; }
    public int? EditedById { get; set; }
    public User? EditedBy { get; set; }
    public DateTime? EditedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
