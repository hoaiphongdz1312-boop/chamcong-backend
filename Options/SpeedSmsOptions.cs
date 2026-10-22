namespace NguyenHoaiPhong_1681.Options;

public class SpeedSmsOptions
{
    public const string SectionName = "SpeedSms";

    public string AccessToken { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.speedsms.vn/index.php";
    public int SmsType { get; set; } = 4;
    public string Sender { get; set; } = "Verify";
    public int TimeoutSeconds { get; set; } = 15;
}
