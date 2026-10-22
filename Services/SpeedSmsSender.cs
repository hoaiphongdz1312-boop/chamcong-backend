using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NguyenHoaiPhong_1681.Helpers;
using NguyenHoaiPhong_1681.Middleware;
using NguyenHoaiPhong_1681.Options;

namespace NguyenHoaiPhong_1681.Services;

/// <summary>Gửi SMS qua SpeedSMS.vn (POST {BaseUrl}/sms/send).</summary>
public class SpeedSmsSender(
    HttpClient http,
    IOptions<SpeedSmsOptions> options,
    IHostEnvironment env,
    ILogger<SpeedSmsSender> logger) : ISmsSender
{
    private const string GenericError = "Không gửi được SMS, vui lòng thử lại hoặc dùng Email";
    private readonly SpeedSmsOptions _opt = options.Value;

    public async Task<SmsSendResult> SendAsync(string phone, string content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.AccessToken))
        {
            throw new AppException(500,
                "Chưa cấu hình SpeedSms:AccessToken (dotnet user-secrets set \"SpeedSms:AccessToken\" ...)");
        }

        var masked = PhoneHelper.Mask(phone);
        var url = _opt.BaseUrl.TrimEnd('/') + "/sms/send";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                to = new[] { phone },
                content,
                sms_type = _opt.SmsType,
                sender = _opt.Sender
            })
        };
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opt.AccessToken}:x"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        HttpResponseMessage response;
        string body;
        try
        {
            response = await http.SendAsync(request, ct);
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            var reason = ex is TaskCanceledException ? $"timeout sau {_opt.TimeoutSeconds}s" : ex.Message;
            logger.LogError("SpeedSMS lỗi kết nối tới {Phone}, sms_type={SmsType}: {Reason}", masked, _opt.SmsType, reason);
            throw new AppException(502, env.IsDevelopment()
                ? $"SpeedSMS: không gọi được API ({reason}). sms_type={_opt.SmsType}, sender={_opt.Sender}"
                : GenericError);
        }

        string? status = null, code = null, message = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            status = ReadString(root, "status");
            code = ReadString(root, "code");
            message = ReadString(root, "message");
        }
        catch (JsonException)
        {
            // body không phải JSON → xử lý như lỗi bên dưới
        }

        using (response)
        {
            logger.LogInformation("SpeedSMS tới {Phone}: HTTP {Http}, sms_type={SmsType}, status={Status}, code={Code}",
                masked, (int)response.StatusCode, _opt.SmsType, status, code);

            if (response.IsSuccessStatusCode && status == "success")
                return new SmsSendResult("SpeedSms", _opt.SmsType, _opt.Sender, body);

            logger.LogError("SpeedSMS thất bại tới {Phone}: HTTP {Http}, code={Code}, message={Message}",
                masked, (int)response.StatusCode, code, message);
            throw new AppException(502, env.IsDevelopment()
                ? $"SpeedSMS lỗi: HTTP {(int)response.StatusCode}, code=\"{code}\", message=\"{message ?? body}\". sms_type={_opt.SmsType}, sender={_opt.Sender}"
                : GenericError);
        }
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var v)
            ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText()
            : null;
}
