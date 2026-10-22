namespace NguyenHoaiPhong_1681.Services;

/// <summary>Chỉ log ra console, dùng khi Sms:Provider = "Console".</summary>
public class ConsoleSmsSender(ILogger<ConsoleSmsSender> logger) : ISmsSender
{
    public Task<SmsSendResult> SendAsync(string phone, string content, CancellationToken ct = default)
    {
        logger.LogWarning("[DEV SMS] {Phone}: {Content}", phone, content);
        return Task.FromResult(new SmsSendResult("Console", null, null, "logged to console"));
    }
}
