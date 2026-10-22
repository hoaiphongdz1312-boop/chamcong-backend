namespace NguyenHoaiPhong_1681.Entities;

public class FaceTemplate
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    /// <summary>float[512] đã chuẩn hóa, lưu dạng byte (little-endian).</summary>
    public byte[] Embedding { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
