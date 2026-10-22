using NguyenHoaiPhong_1681.DTOs;

namespace NguyenHoaiPhong_1681.Services;

public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    Task<ResendOtpResponse> ResendRegisterOtpAsync(string username, CancellationToken ct = default);

    Task<VerifyOtpResponse> VerifyRegisterOtpAsync(string username, string code, CancellationToken ct = default);

    /// <summary>Bước 1: kiểm tra mật khẩu rồi gửi OTP (chưa cấp token).</summary>
    Task<LoginOtpResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);

    Task<ResendOtpResponse> ResendLoginOtpAsync(string username, CancellationToken ct = default);

    /// <summary>Bước 2: đúng OTP → cấp token.</summary>
    Task<AuthResponse> VerifyLoginAsync(LoginVerifyRequest request, CancellationToken ct = default);

    Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default);

    Task LogoutAsync(int userId, string refreshToken, CancellationToken ct = default);

    Task<ForgotPasswordResponse> ForgotPasswordAsync(string emailOrPhone, CancellationToken ct = default);

    Task<MessageResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
}
