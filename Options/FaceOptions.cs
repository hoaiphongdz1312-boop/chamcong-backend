namespace NguyenHoaiPhong_1681.Options;

public class FaceOptions
{
    public const string SectionName = "Face";

    /// <summary>Điểm cosine tối thiểu để đăng nhập thẳng.</summary>
    public float AcceptThreshold { get; set; } = 0.45f;
    /// <summary>Điểm cosine tối thiểu để chuyển sang xác thực OTP (nhỏ hơn AcceptThreshold).</summary>
    public float OtpThreshold { get; set; } = 0.40f;
    /// <summary>Cạnh ngắn tối thiểu của khung mặt (pixel).</summary>
    public int MinFaceSizePx { get; set; } = 120;
    /// <summary>Độ lệch yaw tối thiểu giữa ảnh quay đầu và ảnh nhìn thẳng.</summary>
    public float MinTurnDelta { get; set; } = 0.20f;
    /// <summary>|yaw| tối đa của ảnh nhìn thẳng.</summary>
    public float MaxNeutralYaw { get; set; } = 0.15f;
    /// <summary>Cosine tối thiểu giữa các ảnh của cùng một người (3 ảnh đăng ký, 2 ảnh đăng nhập).</summary>
    public float SamePersonThreshold { get; set; } = 0.5f;
    public int ChallengeSeconds { get; set; } = 30;
    public int MaxFailures { get; set; } = 5;
    public int LockMinutes { get; set; } = 15;
    public int MaxImageBytes { get; set; } = 2097152;
    /// <summary>Số ảnh/mẫu cần khi đăng ký.</summary>
    public int EnrollImageCount { get; set; } = 3;
    /// <summary>Camera trước có thể lật gương nên mặc định không kiểm hướng quay trái/phải.</summary>
    public bool CheckTurnDirection { get; set; } = false;
}
