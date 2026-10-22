namespace NguyenHoaiPhong_1681.Entities;

public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    /// <summary>SHA256 hex của refresh token (không lưu token gốc).</summary>
    public string TokenHash { get; set; } = "";
    public string? DeviceId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
}
