namespace NguyenHoaiPhong_1681.Options;

public class LoginOptions
{
    public const string SectionName = "Login";

    public int MaxFailedPassword { get; set; } = 5;
    public int LockMinutes { get; set; } = 15;
    /// <summary>Số request tối đa mỗi phút mỗi IP cho register*/login*/forgot-password/reset-password.</summary>
    public int AuthRateLimitPerMinute { get; set; } = 10;
    /// <summary>Giới hạn chung mỗi phút mỗi IP cho mọi request (policy "default").</summary>
    public int DefaultRateLimitPerMinute { get; set; } = 120;
}
