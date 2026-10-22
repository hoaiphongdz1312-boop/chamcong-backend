namespace NguyenHoaiPhong_1681.Options;

public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string User { get; set; } = "";
    public string AppPassword { get; set; } = "";
    public string FromName { get; set; } = "";
    public string FromAddress { get; set; } = "";
}
