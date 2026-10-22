using Microsoft.EntityFrameworkCore;
using NguyenHoaiPhong_1681.Data;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Enums;
using NguyenHoaiPhong_1681.Helpers;
using NguyenHoaiPhong_1681.Middleware;

namespace NguyenHoaiPhong_1681.Services;

public interface IProfileService
{
    Task<UserInfoDto> GetAsync(int userId, CancellationToken ct = default);

    Task<UserInfoDto> UpdateAsync(int userId, UpdateMeRequest request, CancellationToken ct = default);

    Task<MessageResponse> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct = default);

    Task<ResendOtpResponse> SendPhoneOtpAsync(int userId, string phone, CancellationToken ct = default);

    Task<UserInfoDto> VerifyPhoneAsync(int userId, string phone, string code, CancellationToken ct = default);
}

public class ProfileService(AppDbContext db, ITokenService tokenService, IOtpService otpService) : IProfileService
{
    public async Task<UserInfoDto> GetAsync(int userId, CancellationToken ct = default) =>
        TokenService.ToUserInfo(await LoadAsync(userId, ct));

    public async Task<UserInfoDto> UpdateAsync(int userId, UpdateMeRequest request, CancellationToken ct = default)
    {
        var user = await LoadAsync(userId, ct);

        string? phone = null;
        if (!string.IsNullOrWhiteSpace(request.Phone) && !PhoneHelper.TryNormalize(request.Phone, out phone))
            throw new AppException(400, "Số điện thoại không hợp lệ (di động Việt Nam 10 số đầu 03/05/07/08/09, hoặc +84/84)");

        if (phone != user.PhoneNumber)
        {
            // Đổi số → phải xác thực lại
            user.PhoneNumber = phone;
            user.PhoneVerified = false;
        }

        user.FullName = request.FullName.Trim();
        user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return TokenService.ToUserInfo(user);
    }

    public async Task<MessageResponse> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await LoadAsync(userId, ct);
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new AppException(400, "Mật khẩu hiện tại không đúng");
        if (request.CurrentPassword == request.NewPassword)
            throw new AppException(400, "Mật khẩu mới phải khác mật khẩu hiện tại");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tokenService.RevokeAllAsync(userId, request.RefreshToken, ct);

        return new MessageResponse("Đổi mật khẩu thành công, các thiết bị khác đã bị đăng xuất");
    }

    public async Task<ResendOtpResponse> SendPhoneOtpAsync(int userId, string phone, CancellationToken ct = default)
    {
        var normalized = NormalizePhone(phone);
        if (await db.Users.AnyAsync(u => u.PhoneNumber == normalized && u.PhoneVerified && u.Id != userId, ct))
            throw new AppException(409, "Số điện thoại đã được sử dụng");

        var sessionId = await otpService.CreateSessionAsync(userId, OtpPurpose.ChangePhone, OtpChannel.Sms, normalized, ct);
        var sent = await otpService.SendAsync(sessionId, ct);
        return new ResendOtpResponse(sent.ExpiresAt, sent.ResendAfterSeconds);
    }

    public async Task<UserInfoDto> VerifyPhoneAsync(int userId, string phone, string code, CancellationToken ct = default)
    {
        var normalized = NormalizePhone(phone);
        var otp = await db.OtpCodes.AsNoTracking()
            .Where(o => o.UserId == userId && o.Purpose == OtpPurpose.ChangePhone && !o.Used)
            .OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new AppException(400, "Không có mã xác thực đang chờ, vui lòng gửi mã mới");
        if (otp.Destination != normalized)
            throw new AppException(400, "Số điện thoại không khớp với số đã nhận mã");

        await otpService.VerifyAsync(otp.SessionId, code, ct);

        var user = await LoadAsync(userId, ct);
        user.PhoneNumber = normalized;
        user.PhoneVerified = true;
        user.UpdatedAt = DateTime.UtcNow;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new AppException(409, "Số điện thoại đã được sử dụng");
        }
        return TokenService.ToUserInfo(user);
    }

    private static string NormalizePhone(string phone) =>
        PhoneHelper.TryNormalize(phone, out var normalized)
            ? normalized
            : throw new AppException(400, "Số điện thoại không hợp lệ (di động Việt Nam 10 số đầu 03/05/07/08/09, hoặc +84/84)");

    private async Task<Entities.User> LoadAsync(int userId, CancellationToken ct) =>
        await db.Users.Include(u => u.Role).Include(u => u.Department).FirstOrDefaultAsync(u => u.Id == userId, ct)
        ?? throw new AppException(401, "Tài khoản không còn tồn tại");
}
