namespace NguyenHoaiPhong_1681.Entities;

public class FaceLoginLog
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Username { get; set; } = "";
    /// <summary>Success | OtpRequired | LowScore | NoFace | LivenessFailed | BadChallenge | Locked.</summary>
    public string Result { get; set; } = "";
    /// <summary>Chỉ để hiệu chỉnh ngưỡng, không trả cho client.</summary>
    public float? Score { get; set; }
    public string? Ip { get; set; }
    public string? DeviceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
