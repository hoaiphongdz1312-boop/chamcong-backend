namespace NguyenHoaiPhong_1681.Services;

public interface IEmailSender
{
    Task SendOtpAsync(string to, string code, int expireMinutes, CancellationToken ct = default);

    Task SendAsync(string to, string subject, string htmlBody, string textBody, CancellationToken ct = default);

    /// <summary>Địa chỉ From thực tế (FromAddress, rỗng thì User).</summary>
    string FromAddress { get; }
}
