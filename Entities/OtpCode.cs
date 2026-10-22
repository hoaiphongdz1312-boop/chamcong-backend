using NguyenHoaiPhong_1681.Enums;

namespace NguyenHoaiPhong_1681.Entities;

public class OtpCode
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid SessionId { get; set; }
    public OtpPurpose Purpose { get; set; }
    public OtpChannel Channel { get; set; }
    /// <summary>Email hoặc số điện thoại 84xxxxxxxxx (chưa che).</summary>
    public string Destination { get; set; } = "";
    /// <summary>SHA256 hex của mã OTP; rỗng khi chưa gửi lần nào.</summary>
    public string CodeHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public int SendCount { get; set; }
    public DateTime? LastSentAt { get; set; }
    public bool Used { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
