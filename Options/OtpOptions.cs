namespace NguyenHoaiPhong_1681.Options;

public class OtpOptions
{
    public const string SectionName = "Otp";

    public bool DevMode { get; set; }
    public int ExpireMinutes { get; set; } = 5;
    public int ResendSeconds { get; set; } = 60;
    public int MaxSendPerSession { get; set; } = 5;
    public int MaxAttempts { get; set; } = 5;
    public int MaxSmsPerPhonePerDay { get; set; } = 5;
}
