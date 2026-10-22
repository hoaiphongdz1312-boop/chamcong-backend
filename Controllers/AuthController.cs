using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Helpers;
using NguyenHoaiPhong_1681.Services;

namespace NguyenHoaiPhong_1681.Controllers;

/// <summary>Đăng ký, đăng nhập 2 bước (mật khẩu + OTP), token, quên mật khẩu.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService, ITokenService tokenService) : ControllerBase
{
    /// <summary>Đăng ký tài khoản (role Employee, chờ duyệt) và gửi OTP qua Email hoặc SMS.</summary>
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request, CancellationToken ct)
        => Ok(await authService.RegisterAsync(request, ct));

    /// <summary>Gửi lại OTP đăng ký theo đúng kênh của phiên.</summary>
    [HttpPost("register/resend")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<ResendOtpResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResendOtpResponse>> Resend(ResendOtpRequest request, CancellationToken ct)
        => Ok(await authService.ResendRegisterOtpAsync(request.Username, ct));

    /// <summary>Xác thực OTP đăng ký.</summary>
    [HttpPost("register/verify")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<VerifyOtpResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<VerifyOtpResponse>> Verify(VerifyOtpRequest request, CancellationToken ct)
        => Ok(await authService.VerifyRegisterOtpAsync(request.Username, request.Code, ct));

    /// <summary>Đăng nhập bước 1: tên đăng nhập (hoặc email) + mật khẩu → gửi OTP qua kênh đã chọn. Chưa cấp token.</summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<LoginOtpResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginOtpResponse>> Login(LoginRequest request, CancellationToken ct)
        => Ok(await authService.LoginAsync(request, ct));

    /// <summary>Gửi lại OTP đăng nhập theo đúng kênh của phiên gần nhất.</summary>
    [HttpPost("login/resend")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<ResendOtpResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResendOtpResponse>> LoginResend(ResendOtpRequest request, CancellationToken ct)
        => Ok(await authService.ResendLoginOtpAsync(request.Username, ct));

    /// <summary>Đăng nhập bước 2: xác thực OTP → access token, refresh token, quyền và homeRoute.</summary>
    [HttpPost("login/verify")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponse>> LoginVerify(LoginVerifyRequest request, CancellationToken ct)
        => Ok(await authService.VerifyLoginAsync(request, ct));

    /// <summary>Đổi refresh token lấy cặp token mới (refresh token cũ bị thu hồi).</summary>
    [HttpPost("refresh")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct)
        => Ok(await authService.RefreshAsync(request.RefreshToken, ct));

    /// <summary>Đăng xuất: thu hồi refresh token của thiết bị này.</summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await authService.LogoutAsync(User.GetUserId(), request.RefreshToken, ct);
        return NoContent();
    }

    /// <summary>Quên mật khẩu: gửi OTP tới email/số điện thoại đã xác thực. Luôn trả 200.</summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<ForgotPasswordResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
        => Ok(await authService.ForgotPasswordAsync(request.EmailOrPhone, ct));

    /// <summary>Đặt lại mật khẩu bằng OTP; thu hồi mọi phiên đăng nhập và mở khóa tạm.</summary>
    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MessageResponse>> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
        => Ok(await authService.ResetPasswordAsync(request, ct));

    /// <summary>Thông tin user hiện tại, danh sách quyền và homeRoute.</summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken ct)
        => Ok(await tokenService.BuildMeAsync(User.GetUserId(), ct));
}
