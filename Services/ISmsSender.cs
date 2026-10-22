namespace NguyenHoaiPhong_1681.Services;

public record SmsSendResult(string Provider, int? SmsType, string? Sender, string? RawResponse);

public interface ISmsSender
{
    /// <param name="phone">Số đã chuẩn hóa 84xxxxxxxxx.</param>
    /// <param name="content">Nội dung KHÔNG DẤU.</param>
    /// <param name="ct">Token hủy.</param>
    Task<SmsSendResult> SendAsync(string phone, string content, CancellationToken ct = default);
}
