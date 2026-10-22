using Microsoft.AspNetCore.Mvc;

namespace NguyenHoaiPhong_1681.Middleware;

/// <summary>Bắt mọi exception, trả ProblemDetails có field "message" tiếng Việt.</summary>
public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            if (ex.StatusCode >= 500) logger.LogError("AppException {Status}: {Message}", ex.StatusCode, ex.Message);
            else logger.LogInformation("AppException {Status}: {Message}", ex.StatusCode, ex.Message);

            await WriteAsync(context, ex.StatusCode, ex.Message, ex.Extensions);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client đã hủy request
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi không xử lý được: {Path}", context.Request.Path);
            var message = env.IsDevelopment()
                ? $"Lỗi hệ thống: [{ex.GetType().Name}] {ex.Message}"
                : "Lỗi hệ thống, vui lòng thử lại sau";
            await WriteAsync(context, StatusCodes.Status500InternalServerError, message, null);
        }
    }

    private static Task WriteAsync(HttpContext context, int status, string message, IDictionary<string, object?>? extensions) =>
        WriteProblemAsync(context, status, message, extensions);

    /// <summary>Ghi ProblemDetails có field "message" tiếng Việt (dùng chung cho JwtBearer, rate limiter).</summary>
    public static async Task WriteProblemAsync(HttpContext context, int status, string message, IDictionary<string, object?>? extensions = null)
    {
        if (context.Response.HasStarted) return;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = ReasonPhrase(status),
            Detail = message,
            Instance = context.Request.Path
        };
        problem.Extensions["message"] = message;
        if (extensions is not null)
            foreach (var (key, value) in extensions) problem.Extensions[key] = value;

        context.Response.Clear();
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
    }

    private static string ReasonPhrase(int status) => status switch
    {
        400 => "Yêu cầu không hợp lệ",
        401 => "Chưa xác thực",
        403 => "Không có quyền truy cập",
        404 => "Không tìm thấy",
        409 => "Xung đột dữ liệu",
        423 => "Tài khoản tạm khóa",
        429 => "Quá nhiều yêu cầu",
        502 => "Lỗi dịch vụ bên ngoài",
        _ when status >= 500 => "Lỗi máy chủ",
        _ => "Lỗi"
    };
}
