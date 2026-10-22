using System.ComponentModel.DataAnnotations;

namespace NguyenHoaiPhong_1681.DTOs;

/// <summary>Quy tắc validate dùng chung.</summary>
public static class ValidationRules
{
    public const string PasswordPattern = @"^(?=.*[A-Za-z])(?=.*\d).+$";
    public const string PasswordMessage = "Mật khẩu phải có cả chữ và số";
    public const string ChannelPattern = @"^(?i)(email|sms)$";
    public const string ChannelMessage = "Kênh nhận mã chỉ nhận \"Email\" hoặc \"Sms\"";
}

public class LoginRequest
{
    /// <summary>Tên đăng nhập hoặc email.</summary>
    [Required(ErrorMessage = "Tên đăng nhập hoặc email là bắt buộc")]
    [StringLength(256, ErrorMessage = "Tên đăng nhập hoặc email quá dài")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
    [StringLength(100, ErrorMessage = "Mật khẩu quá dài")]
    public string Password { get; set; } = "";

    /// <summary>"Email" (mặc định) hoặc "Sms".</summary>
    [RegularExpression(ValidationRules.ChannelPattern, ErrorMessage = ValidationRules.ChannelMessage)]
    public string? Channel { get; set; } = "Email";
}

public record LoginOtpResponse(
    string Username,
    string Channel,
    string MaskedDestination,
    DateTime ExpiresAt,
    int ResendAfterSeconds);

public class LoginVerifyRequest
{
    [Required(ErrorMessage = "Tên đăng nhập là bắt buộc")]
    [StringLength(50, ErrorMessage = "Tên đăng nhập tối đa 50 ký tự")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Mã xác thực là bắt buộc")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã xác thực gồm 6 chữ số")]
    public string Code { get; set; } = "";

    /// <summary>Mã thiết bị (tùy chọn) để quản lý phiên đăng nhập.</summary>
    [StringLength(100, ErrorMessage = "deviceId tối đa 100 ký tự")]
    public string? DeviceId { get; set; }
}

public class RefreshRequest
{
    [Required(ErrorMessage = "refreshToken là bắt buộc")]
    [StringLength(200, ErrorMessage = "refreshToken không hợp lệ")]
    public string RefreshToken { get; set; } = "";
}

public class ForgotPasswordRequest
{
    /// <summary>Email hoặc số điện thoại đã xác thực.</summary>
    [Required(ErrorMessage = "Email hoặc số điện thoại là bắt buộc")]
    [StringLength(256, ErrorMessage = "Email hoặc số điện thoại quá dài")]
    public string EmailOrPhone { get; set; } = "";
}

public record ForgotPasswordResponse(string Message, string? Username);

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "Tên đăng nhập là bắt buộc")]
    [StringLength(50, ErrorMessage = "Tên đăng nhập tối đa 50 ký tự")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Mã xác thực là bắt buộc")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã xác thực gồm 6 chữ số")]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự")]
    [RegularExpression(ValidationRules.PasswordPattern, ErrorMessage = ValidationRules.PasswordMessage)]
    public string NewPassword { get; set; } = "";
}

public record MessageResponse(string Message);

public record UserInfoDto(
    int Id,
    string Username,
    string FullName,
    string Email,
    string? PhoneNumber,
    string? AvatarUrl,
    string Role,
    string Status,
    int? DepartmentId,
    string? DepartmentName,
    bool EmailVerified,
    bool PhoneVerified);

public record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    UserInfoDto User,
    IReadOnlyCollection<string> Permissions,
    string HomeRoute);

public record MeResponse(UserInfoDto User, IReadOnlyCollection<string> Permissions, string HomeRoute);

public class UpdateMeRequest
{
    [Required(ErrorMessage = "Họ tên là bắt buộc")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Họ tên tối đa 100 ký tự")]
    public string FullName { get; set; } = "";

    [StringLength(500, ErrorMessage = "Đường dẫn ảnh tối đa 500 ký tự")]
    [Url(ErrorMessage = "Đường dẫn ảnh không hợp lệ")]
    public string? AvatarUrl { get; set; }

    /// <summary>Số di động VN; đổi số → phải xác thực lại.</summary>
    public string? Phone { get; set; }
}

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "Mật khẩu hiện tại là bắt buộc")]
    [StringLength(100, ErrorMessage = "Mật khẩu quá dài")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự")]
    [RegularExpression(ValidationRules.PasswordPattern, ErrorMessage = ValidationRules.PasswordMessage)]
    public string NewPassword { get; set; } = "";

    /// <summary>Refresh token của thiết bị hiện tại (giữ lại, thu hồi các thiết bị khác).</summary>
    public string? RefreshToken { get; set; }
}

public class PhoneSendOtpRequest
{
    [Required(ErrorMessage = "Số điện thoại là bắt buộc")]
    [StringLength(20, ErrorMessage = "Số điện thoại quá dài")]
    public string Phone { get; set; } = "";
}

public class PhoneVerifyRequest
{
    [Required(ErrorMessage = "Số điện thoại là bắt buộc")]
    [StringLength(20, ErrorMessage = "Số điện thoại quá dài")]
    public string Phone { get; set; } = "";

    [Required(ErrorMessage = "Mã xác thực là bắt buộc")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã xác thực gồm 6 chữ số")]
    public string Code { get; set; } = "";
}
