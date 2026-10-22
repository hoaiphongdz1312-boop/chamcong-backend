namespace NguyenHoaiPhong_1681.Middleware;

/// <summary>Lỗi nghiệp vụ có mã HTTP và thông báo tiếng Việt trả thẳng cho client.</summary>
public class AppException(int statusCode, string message, IDictionary<string, object?>? extensions = null)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    /// <summary>Field bổ sung đưa vào ProblemDetails (vd: attemptsLeft).</summary>
    public IDictionary<string, object?> Extensions { get; } = extensions ?? new Dictionary<string, object?>();
}
