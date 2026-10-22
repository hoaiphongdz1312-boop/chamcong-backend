using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NguyenHoaiPhong_1681.Data;
using NguyenHoaiPhong_1681.Entities;
using NguyenHoaiPhong_1681.Enums;
using NguyenHoaiPhong_1681.Helpers;
using NguyenHoaiPhong_1681.Middleware;
using NguyenHoaiPhong_1681.Options;

namespace NguyenHoaiPhong_1681.Services;

public class OtpService(
    AppDbContext db,
    IEmailSender emailSender,
    ISmsSender smsSender,
    IServiceScopeFactory scopeFactory,
    IOptions<OtpOptions> options,
    ILogger<OtpService> logger) : IOtpService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private readonly OtpOptions _opt = options.Value;

    public async Task<Guid> CreateSessionAsync(int userId, OtpPurpose purpose, OtpChannel channel, string destination, CancellationToken ct = default)
    {
        // Phiên mới làm các phiên cũ (chưa dùng) cùng mục đích hết hiệu lực
        var oldSessions = await db.OtpCodes
            .Where(o => o.UserId == userId && o.Purpose == purpose && !o.Used)
            .ToListAsync(ct);
        foreach (var old in oldSessions) old.Used = true;

        var now = DateTime.UtcNow;
        var otp = new OtpCode
        {
            UserId = userId,
            SessionId = Guid.NewGuid(),
            Purpose = purpose,
            Channel = channel,
            Destination = destination,
            ExpiresAt = now,
            CreatedAt = now
        };
        db.OtpCodes.Add(otp);
        await db.SaveChangesAsync(ct);
        return otp.SessionId;
    }

    public async Task<OtpSendResult> SendAsync(Guid sessionId, CancellationToken ct = default)
    {
        var otp = await FindActiveSessionAsync(sessionId, ct);
        var now = DateTime.UtcNow;

        if (otp.LastSentAt is { } lastSent)
        {
            var wait = _opt.ResendSeconds - (int)Math.Floor((now - lastSent).TotalSeconds);
            if (wait > 0)
                throw new AppException(429, $"Vui lòng đợi {wait} giây",
                    new Dictionary<string, object?> { ["retryAfterSeconds"] = wait });
        }

        if (otp.SendCount >= _opt.MaxSendPerSession)
            throw new AppException(429, $"Đã gửi mã quá {_opt.MaxSendPerSession} lần cho phiên này, vui lòng thực hiện lại từ đầu");

        var maskedDestination = Mask(otp.Channel, otp.Destination);

        if (otp.Channel == OtpChannel.Sms)
        {
            // Ngày tính theo giờ Việt Nam (UTC+7)
            var startOfDayUtc = (now + VietnamOffset).Date - VietnamOffset;
            var sentToday = await db.OtpSendLogs.CountAsync(l =>
                l.Channel == OtpChannel.Sms && l.Success
                && l.Destination == maskedDestination && l.CreatedAt >= startOfDayUtc, ct);
            if (sentToday >= _opt.MaxSmsPerPhonePerDay)
                throw new AppException(429, "Số điện thoại đã đạt giới hạn tin nhắn trong ngày");
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        if (_opt.DevMode)
            logger.LogWarning("[DEV OTP] {Destination}: {Code}", otp.Destination, code);

        try
        {
            if (otp.Channel == OtpChannel.Email)
            {
                await emailSender.SendOtpAsync(otp.Destination, code, _opt.ExpireMinutes, ct);
            }
            else
            {
                var content = $"Ma xac thuc cham cong cua ban la {code}. Het han sau {_opt.ExpireMinutes} phut. Khong chia se ma nay.";
                await smsSender.SendAsync(otp.Destination, content, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            await WriteSendLogAsync(otp.UserId, otp.Channel, maskedDestination, false, ex.Message);
            throw;
        }

        await WriteSendLogAsync(otp.UserId, otp.Channel, maskedDestination, true, null);

        // Ghi đè hash → mã cũ của phiên lập tức hết hiệu lực
        otp.CodeHash = Hash(code);
        otp.ExpiresAt = now.AddMinutes(_opt.ExpireMinutes);
        otp.Attempts = 0;
        otp.SendCount++;
        otp.LastSentAt = now;
        await db.SaveChangesAsync(ct);

        return new OtpSendResult(otp.SessionId, otp.Channel, otp.Destination, otp.ExpiresAt, _opt.ResendSeconds);
    }

    public async Task<OtpVerifyResult> VerifyAsync(Guid sessionId, string code, CancellationToken ct = default)
    {
        var otp = await FindActiveSessionAsync(sessionId, ct);
        var now = DateTime.UtcNow;

        var valid = !string.IsNullOrEmpty(otp.CodeHash)
                    && otp.ExpiresAt > now
                    && CryptographicOperations.FixedTimeEquals(
                        SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim())),
                        Convert.FromHexString(otp.CodeHash));

        if (!valid)
        {
            otp.Attempts++;
            if (otp.Attempts >= _opt.MaxAttempts)
            {
                // Hủy mã hiện tại; người dùng phải gửi lại mã mới
                otp.CodeHash = "";
                otp.ExpiresAt = now;
                await db.SaveChangesAsync(ct);
                throw new AppException(400, "Nhập sai quá số lần, vui lòng gửi lại mã",
                    new Dictionary<string, object?> { ["attemptsLeft"] = 0 });
            }

            await db.SaveChangesAsync(ct);
            throw new AppException(400, "Mã không đúng hoặc đã hết hạn",
                new Dictionary<string, object?> { ["attemptsLeft"] = _opt.MaxAttempts - otp.Attempts });
        }

        otp.Used = true;
        await db.SaveChangesAsync(ct);
        return new OtpVerifyResult(otp.UserId, otp.Channel);
    }

    private async Task<OtpCode> FindActiveSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var otp = await db.OtpCodes.FirstOrDefaultAsync(o => o.SessionId == sessionId, ct);
        if (otp is null || otp.Used)
            throw new AppException(400, "Phiên xác thực không tồn tại hoặc đã kết thúc, vui lòng thực hiện lại từ đầu");
        return otp;
    }

    /// <summary>
    /// Ghi log bằng DbContext riêng để log vẫn còn khi transaction đăng ký bị rollback.
    /// </summary>
    private async Task WriteSendLogAsync(int userId, OtpChannel channel, string maskedDestination, bool success, string? error)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var logDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            logDb.OtpSendLogs.Add(new OtpSendLog
            {
                UserId = userId,
                Channel = channel,
                Destination = maskedDestination,
                Success = success,
                Error = error is { Length: > 1000 } ? error[..1000] : error,
                CreatedAt = DateTime.UtcNow
            });
            await logDb.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Không nuốt lỗi khi gửi thành công (log này dùng để đếm giới hạn SMS/ngày)
            logger.LogError(ex, "Không ghi được OtpSendLog cho user {UserId}", userId);
            if (success) throw;
        }
    }

    public static string Mask(OtpChannel channel, string destination) =>
        channel == OtpChannel.Email ? MaskHelper.MaskEmail(destination) : PhoneHelper.Mask(destination);

    private static string Hash(string code) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
