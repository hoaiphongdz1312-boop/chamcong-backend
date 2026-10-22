using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NguyenHoaiPhong_1681.Authorization;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Helpers;
using NguyenHoaiPhong_1681.Services;

namespace NguyenHoaiPhong_1681.Controllers;

/// <summary>Hồ sơ cá nhân của user đang đăng nhập.</summary>
[ApiController]
[Route("api/me")]
[HasPermission(PermissionCodes.ProfileSelf)]
public class MeController(IProfileService profileService) : ControllerBase
{
    /// <summary>Xem hồ sơ.</summary>
    [HttpGet]
    public async Task<ActionResult<UserInfoDto>> Get(CancellationToken ct)
        => Ok(await profileService.GetAsync(User.GetUserId(), ct));

    /// <summary>Sửa họ tên, ảnh đại diện, số điện thoại (đổi số → phải xác thực lại).</summary>
    [HttpPut]
    public async Task<ActionResult<UserInfoDto>> Update(UpdateMeRequest request, CancellationToken ct)
        => Ok(await profileService.UpdateAsync(User.GetUserId(), request, ct));

    /// <summary>Đổi mật khẩu; thu hồi refresh token của các thiết bị khác.</summary>
    [HttpPost("change-password")]
    public async Task<ActionResult<MessageResponse>> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
        => Ok(await profileService.ChangePasswordAsync(User.GetUserId(), request, ct));

    /// <summary>Gửi OTP SMS tới số điện thoại mới.</summary>
    [HttpPost("phone/send-otp")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ResendOtpResponse>> SendPhoneOtp(PhoneSendOtpRequest request, CancellationToken ct)
        => Ok(await profileService.SendPhoneOtpAsync(User.GetUserId(), request.Phone, ct));

    /// <summary>Xác thực OTP: đúng thì cập nhật số điện thoại và phoneVerified = true.</summary>
    [HttpPost("phone/verify")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<UserInfoDto>> VerifyPhone(PhoneVerifyRequest request, CancellationToken ct)
        => Ok(await profileService.VerifyPhoneAsync(User.GetUserId(), request.Phone, request.Code, ct));
}
