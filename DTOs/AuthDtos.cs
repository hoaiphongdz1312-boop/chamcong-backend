using System.ComponentModel.DataAnnotations;
using NguyenHoaiPhong_1681.Helpers;

namespace NguyenHoaiPhong_1681.DTOs;

public class RegisterRequest : IValidatableObject
{
    [Required(ErrorMessage = "Tên đăng nhập là bắt buộc")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải từ 3 đến 50 ký tự")]
    [RegularExpression(@"^[a-zA-Z0-9._]+$", ErrorMessage = "Tên đăng nhập chỉ gồm chữ không dấu, số, dấu chấm và gạch dưới")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(256, ErrorMessage = "Email tối đa 256 ký tự")]
    public string Email { get; set; } = "";

    /// <summary>Tùy chọn; bắt buộc khi verifyChannel = "Sms". Nhận 0xxxxxxxxx, 84xxxxxxxxx, +84xxxxxxxxx.</summary>
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Mật khẩu phải có cả chữ và số")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Họ tên là bắt buộc")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Họ tên tối đa 100 ký tự")]
    public string FullName { get; set; } = "";

    /// <summary>"Email" (mặc định) hoặc "Sms".</summary>
    [RegularExpression(@"^(?i)(email|sms)$", ErrorMessage = "Kênh xác thực chỉ nhận \"Email\" hoặc \"Sms\"")]
    public string? VerifyChannel { get; set; } = "Email";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var isSms = string.Equals(VerifyChannel, "Sms", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(Phone))
        {
            if (isSms)
                yield return new ValidationResult("Số điện thoại là bắt buộc khi xác thực qua SMS", [nameof(Phone)]);
        }
        else if (!PhoneHelper.IsValid(Phone))
        {
            yield return new ValidationResult(
                "Số điện thoại không hợp lệ (di động Việt Nam 10 số đầu 03/05/07/08/09, hoặc +84/84)", [nameof(Phone)]);
        }
    }
}

public record RegisterResponse(
    string Username,
    string Channel,
    string MaskedDestination,
    DateTime ExpiresAt,
    int ResendAfterSeconds);

public class ResendOtpRequest
{
    [Required(ErrorMessage = "Tên đăng nhập là bắt buộc")]
    [StringLength(50, ErrorMessage = "Tên đăng nhập tối đa 50 ký tự")]
    public string Username { get; set; } = "";
}

public record ResendOtpResponse(DateTime ExpiresAt, int ResendAfterSeconds);

public class VerifyOtpRequest
{
    [Required(ErrorMessage = "Tên đăng nhập là bắt buộc")]
    [StringLength(50, ErrorMessage = "Tên đăng nhập tối đa 50 ký tự")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Mã xác thực là bắt buộc")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã xác thực gồm 6 chữ số")]
    public string Code { get; set; } = "";
}

public record VerifyOtpResponse(string Message, string Status);

