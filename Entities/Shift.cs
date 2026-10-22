namespace NguyenHoaiPhong_1681.Entities;

public class Shift
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public TimeOnly StartTime { get; set; }
    /// <summary>Nhỏ hơn StartTime nghĩa là ca qua đêm.</summary>
    public TimeOnly EndTime { get; set; }
    public int LateToleranceMinutes { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
