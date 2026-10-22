using NguyenHoaiPhong_1681.DTOs;

namespace NguyenHoaiPhong_1681.Services;

/// <summary>Đăng nhập khuôn mặt: Auth có giá trị khi đạt ngưỡng, Otp có giá trị khi rơi vào vùng xám.</summary>
public record FaceLoginResult(AuthResponse? Auth, LoginOtpResponse? Otp);

public interface IFaceLoginService
{
    Task<FaceEnrollResponse> EnrollAsync(int userId, FaceEnrollForm form, CancellationToken ct = default);

    Task<FaceStatusResponse> StatusAsync(int userId, CancellationToken ct = default);

    Task DeleteAsync(int userId, CancellationToken ct = default);

    FaceChallengeResponse CreateChallenge(FaceChallengeRequest request);

    Task<FaceLoginResult> LoginAsync(FaceLoginForm form, string? ip, CancellationToken ct = default);
}
