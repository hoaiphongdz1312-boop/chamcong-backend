    namespace NguyenHoaiPhong_1681.Options;

public class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary>"Console" | "SpeedSms"</summary>
    public string Provider { get; set; } = "Console";
}