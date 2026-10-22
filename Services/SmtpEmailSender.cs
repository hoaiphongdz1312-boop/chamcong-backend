using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using NguyenHoaiPhong_1681.Helpers;
using NguyenHoaiPhong_1681.Middleware;
using NguyenHoaiPhong_1681.Options;

namespace NguyenHoaiPhong_1681.Services;

public class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    IHostEnvironment env,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _opt = options.Value;

    public string FromAddress => string.IsNullOrWhiteSpace(_opt.FromAddress) ? _opt.User : _opt.FromAddress;

    public Task SendOtpAsync(string to, string code, int expireMinutes, CancellationToken ct = default)
    {
        var spaced = string.Join(' ', code.ToCharArray());
        var html = $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;max-width:480px;margin:auto;padding:24px;border:1px solid #e5e7eb;border-radius:12px">
              <h2 style="margin:0 0 12px;color:#111827">Xác thực tài khoản chấm công</h2>
              <p style="color:#374151">Mã xác thực (OTP) của bạn là:</p>
              <p style="font-size:36px;font-weight:700;letter-spacing:12px;color:#1d4ed8;text-align:center;margin:24px 0">{code}</p>
              <p style="color:#374151">Mã có hiệu lực trong <b>{expireMinutes} phút</b>. Không chia sẻ mã này cho bất kỳ ai.</p>
              <p style="color:#9ca3af;font-size:12px">Nếu bạn không yêu cầu mã này, hãy bỏ qua email.</p>
            </div>
            """;
        var text = $"Ma xac thuc cham cong cua ban la: {spaced}\nMa het han sau {expireMinutes} phut. Khong chia se ma nay cho bat ky ai.";

        return SendAsync(to, $"Mã xác thực chấm công: {code}", html, text, ct);
    }

    public async Task SendAsync(string to, string subject, string htmlBody, string textBody, CancellationToken ct = default)
    {
        var appPassword = (_opt.AppPassword ?? "").Replace(" ", "");
        if (string.IsNullOrWhiteSpace(_opt.User) || string.IsNullOrWhiteSpace(appPassword))
        {
            throw new AppException(500,
                "Chưa cấu hình Smtp:User / Smtp:AppPassword. Chạy: dotnet user-secrets set \"Smtp:User\" ... và dotnet user-secrets set \"Smtp:AppPassword\" ...");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_opt.FromName, FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody, TextBody = textBody }.ToMessageBody();

        using var client = new SmtpClient { Timeout = 20_000 };
        try
        {
            await client.ConnectAsync(_opt.Host, _opt.Port, SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(_opt.User, appPassword, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
            logger.LogInformation("Đã gửi email tới {To} qua {Host}:{Port}", MaskHelper.MaskEmail(to), _opt.Host, _opt.Port);
        }
        catch (AuthenticationException ex)
        {
            throw AuthFailed(ex);
        }
        catch (SmtpCommandException ex) when ((int)ex.StatusCode == 535)
        {
            throw AuthFailed(ex);
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException
                                   || (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            logger.LogError(ex, "Không kết nối được SMTP {Host}:{Port}", _opt.Host, _opt.Port);
            throw new AppException(502, $"Không kết nối được {_opt.Host}:{_opt.Port} (mạng chặn cổng?).");
        }
        catch (Exception ex) when (ex is not AppException && ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Lỗi gửi email tới {To}", MaskHelper.MaskEmail(to));
            var msg = "Gửi email thất bại, vui lòng thử lại sau.";
            if (env.IsDevelopment()) msg += $" [{ex.GetType().Name}] {ex.Message}";
            throw new AppException(502, msg);
        }
    }

    private AppException AuthFailed(Exception ex)
    {
        var masked = MaskHelper.MaskEmail(_opt.User);
        logger.LogError(ex, "SMTP từ chối đăng nhập cho {User}", masked);
        return new AppException(502,
            $"Máy chủ email từ chối đăng nhập (535): mật khẩu/key sai hoặc không thuộc tài khoản {masked}.");
    }
}
