namespace NguyenHoaiPhong_1681.Options;

public class AuthOptions
{
    public const string SectionName = "Auth";

    public bool AutoApprove { get; set; }

    /// <summary>Tài khoản chưa xác thực sau số phút này sẽ bị xóa khỏi database.</summary>
    public int UnverifiedExpireMinutes { get; set; } = 1;
}
