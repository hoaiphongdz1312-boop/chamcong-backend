using NguyenHoaiPhong_1681.Enums;

namespace NguyenHoaiPhong_1681.Services;

public record OtpSendResult(Guid SessionId, OtpChannel Channel, string Destination, DateTime ExpiresAt, int ResendAfterSeconds);

public record OtpVerifyResult(int UserId, OtpChannel Channel);

public interface IOtpService
{
    Task<Guid> CreateSessionAsync(int userId, OtpPurpose purpose, OtpChannel channel, string destination, CancellationToken ct = default);

    Task<OtpSendResult> SendAsync(Guid sessionId, CancellationToken ct = default);

    Task<OtpVerifyResult> VerifyAsync(Guid sessionId, string code, CancellationToken ct = default);
}
