using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NguyenHoaiPhong_1681.Helpers;
using NguyenHoaiPhong_1681.Middleware;
using NguyenHoaiPhong_1681.Options;
using NguyenHoaiPhong_1681.Services;

namespace NguyenHoaiPhong_1681.Controllers;

/// <summary>API kiểm tra cấu hình gửi Email/SMS. Chỉ được map ở môi trường Development.</summary>
[ApiController]
[Route("api/dev")]
public class DevController(
    IEmailSender emailSender,
    ISmsSender smsSender,
    IOptions<SmtpOptions> smtpOptions,
    IOptions<SmsOptions> smsOptions) : ControllerBase
{
    [HttpGet("smtp-test")]
    public async Task<IActionResult> SmtpTest(
        [FromQuery, Required(ErrorMessage = "Thiếu tham số to"), EmailAddress(ErrorMessage = "Email không hợp lệ")] string to,
        CancellationToken ct)
    {
        const string text = "SMTP OK NguyenHoaiPhong_1681";
        await emailSender.SendAsync(to, text, $"<h2>{text}</h2><p>Gửi lúc {DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>", text, ct);
        return Ok(new { ok = true, from = emailSender.FromAddress, host = $"{smtpOptions.Value.Host}:{smtpOptions.Value.Port}" });
    }

    [HttpGet("sms-test")]
    public async Task<IActionResult> SmsTest(
        [FromQuery, Required(ErrorMessage = "Thiếu tham số phone")] string phone,
        CancellationToken ct)
    {
        if (!PhoneHelper.TryNormalize(phone, out var normalized))
            throw new AppException(400, "Số điện thoại không hợp lệ (di động Việt Nam 10 số đầu 03/05/07/08/09, hoặc +84/84)");

        var result = await smsSender.SendAsync(normalized, "SMS OK NguyenHoaiPhong_1681", ct);
        return Ok(new
        {
            ok = true,
            provider = smsOptions.Value.Provider,
            smsType = result.SmsType,
            sender = result.Sender,
            response = result.RawResponse
        });
    }
}
