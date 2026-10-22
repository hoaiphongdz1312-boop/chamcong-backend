using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NguyenHoaiPhong_1681.Authorization;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Helpers;
using NguyenHoaiPhong_1681.Services;

namespace NguyenHoaiPhong_1681.Controllers;

/// <summary>Đăng ký và đăng nhập bằng khuôn mặt.</summary>
[ApiController]
public class FaceController(IFaceLoginService faceLoginService) : ControllerBase
{
    /// <summary>Trần dung lượng body multipart (3 ảnh đăng ký + trường form); từng ảnh còn bị giới hạn bởi Face:MaxImageBytes.</summary>
    private const long MaxBodyBytes = 7 * 1024 * 1024;

    /// <summary>Đăng ký khuôn mặt: mật khẩu hiện tại + đúng 3 ảnh (thẳng / hơi trái / hơi phải). Thay thế mẫu cũ.</summary>
    [HttpPost("api/face/enroll")]
    [HasPermission(PermissionCodes.ProfileSelf)]
    [EnableRateLimiting("auth")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxBodyBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBodyBytes)]
    [ProducesResponseType<FaceEnrollResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FaceEnrollResponse>> Enroll([FromForm] FaceEnrollForm form, CancellationToken ct)
        => Ok(await faceLoginService.EnrollAsync(User.GetUserId(), form, ct));

    /// <summary>Trạng thái đăng ký khuôn mặt của tôi.</summary>
    [HttpGet("api/face/status")]
    [Authorize]
    [ProducesResponseType<FaceStatusResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FaceStatusResponse>> Status(CancellationToken ct)
        => Ok(await faceLoginService.StatusAsync(User.GetUserId(), ct));

    /// <summary>Xóa khuôn mặt đã đăng ký của tôi.</summary>
    [HttpDelete("api/face")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        await faceLoginService.DeleteAsync(User.GetUserId(), ct);
        return NoContent();
    }

    /// <summary>Xin phiên quét mặt (30 giây, dùng một lần). Luôn trả 200 cùng dạng dù username có tồn tại hay không.</summary>
    [HttpPost("api/face/challenge")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<FaceChallengeResponse>(StatusCodes.Status200OK)]
    public ActionResult<FaceChallengeResponse> Challenge(FaceChallengeRequest request)
        => Ok(faceLoginService.CreateChallenge(request));

    /// <summary>Đăng nhập bằng khuôn mặt: 200 = AuthResponse, 202 = LoginOtpResponse (cần OTP qua /api/auth/login/verify).</summary>
    [HttpPost("api/auth/login-face")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxBodyBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBodyBytes)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<LoginOtpResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> LoginFace([FromForm] FaceLoginForm form, CancellationToken ct)
    {
        var result = await faceLoginService.LoginAsync(form, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.Auth is not null ? Ok(result.Auth) : StatusCode(StatusCodes.Status202Accepted, result.Otp);
    }
}
