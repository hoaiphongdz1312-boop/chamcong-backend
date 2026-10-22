using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NguyenHoaiPhong_1681.Data;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Entities;
using NguyenHoaiPhong_1681.Enums;
using NguyenHoaiPhong_1681.Helpers;
using NguyenHoaiPhong_1681.Middleware;
using NguyenHoaiPhong_1681.Options;

namespace NguyenHoaiPhong_1681.Services;

public class AuthService(
    AppDbContext db,
    IOtpService otpService,
    ITokenService tokenService,
    IOptions<AuthOptions> authOptions,
    IOptions<LoginOptions> loginOptions,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var channel = string.Equals(request.VerifyChannel, "Sms", StringComparison.OrdinalIgnoreCase)
            ? OtpChannel.Sms
            : OtpChannel.Email;
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        string? phone = null;
        if (!string.IsNullOrWhiteSpace(request.Phone) && !PhoneHelper.TryNormalize(request.Phone, out phone))
            throw new AppException(400, "Số điện thoại không hợp lệ");

        var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        // Tài khoản đã xác thực qua bất kỳ kênh nào thì không cho đăng ký đè
        if (existing is not null && (existing.EmailVerified || existing.PhoneVerified || existing.Status != UserStatus.PendingApproval))
            throw new AppException(409, "Email đã được sử dụng");

        var existingId = existing?.Id ?? 0;
        if (await db.Users.AnyAsync(u => u.Username == username && u.Id != existingId, ct))
            throw new AppException(409, "Tên đăng nhập đã tồn tại");

        if (phone is not null && await db.Users.AnyAsync(u => u.PhoneNumber == phone && u.PhoneVerified && u.Id != existingId, ct))
            throw new AppException(409, "Số điện thoại đã được sử dụng");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var user = existing ?? new User { Email = email, CreatedAt = DateTime.UtcNow };
            user.Username = username;
            user.PhoneNumber = phone;
            user.PhoneVerified = false;
            user.EmailVerified = false;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            user.FullName = request.FullName.Trim();
            user.RoleId = await db.Roles.Where(r => r.Code == Role.Employee).Select(r => r.Id).SingleAsync(ct);
            user.Status = UserStatus.PendingApproval;
            if (existing is null) db.Users.Add(user);
            await db.SaveChangesAsync(ct);

            var destination = channel == OtpChannel.Email ? email : phone!;
            var sessionId = await otpService.CreateSessionAsync(user.Id, OtpPurpose.RegisterVerify, channel, destination, ct);
            var sent = await otpService.SendAsync(sessionId, ct);

            await tx.CommitAsync(ct);
            logger.LogInformation("Đăng ký user {UserId} ({Username}), xác thực qua {Channel}", user.Id, username, channel);

            return new RegisterResponse(
                username,
                channel.ToString(),
                OtpService.Mask(channel, destination),
                sent.ExpiresAt,
                sent.ResendAfterSeconds);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw new AppException(409, "Email, tên đăng nhập hoặc số điện thoại đã được sử dụng");
        }
        catch
        {
            // Gửi OTP thất bại → không để lại user mới / OtpCode; lỗi của sender được ném nguyên vẹn
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<ResendOtpResponse> ResendRegisterOtpAsync(string username, CancellationToken ct = default)
    {
        var sessionId = await FindSessionAsync(username, OtpPurpose.RegisterVerify, ct);
        var sent = await otpService.SendAsync(sessionId, ct);
        return new ResendOtpResponse(sent.ExpiresAt, sent.ResendAfterSeconds);
    }

    public async Task<VerifyOtpResponse> VerifyRegisterOtpAsync(string username, string code, CancellationToken ct = default)
    {
        var sessionId = await FindSessionAsync(username, OtpPurpose.RegisterVerify, ct);
        var result = await otpService.VerifyAsync(sessionId, code, ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == result.UserId, ct)
                   ?? throw new AppException(404, "Không tìm thấy tài khoản");

        if (result.Channel == OtpChannel.Email) user.EmailVerified = true;
        else user.PhoneVerified = true;

        if (authOptions.Value.AutoApprove && user.Status == UserStatus.PendingApproval)
            user.Status = UserStatus.Active;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new AppException(409, "Số điện thoại đã được sử dụng");
        }

        var message = user.Status == UserStatus.Active
            ? "Xác thực thành công, tài khoản đã được kích hoạt"
            : "Xác thực thành công, tài khoản đang chờ quản trị viên duyệt";
        return new VerifyOtpResponse(message, user.Status.ToString());
    }

    public async Task<LoginOtpResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var login = request.Username.Trim();
        var email = login.ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == login || u.Email == email, ct)
                   ?? throw new AppException(401, WrongCredentialsMessage);

        var now = DateTime.UtcNow;
        if (user.LockoutUntil > now)
            throw LockedOut(user.LockoutUntil.Value);

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            var opt = loginOptions.Value;
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= opt.MaxFailedPassword)
            {
                user.FailedLoginCount = 0;
                user.LockoutUntil = now.AddMinutes(opt.LockMinutes);
                await db.SaveChangesAsync(ct);
                logger.LogWarning("Khóa tạm user {UserId} {Minutes} phút vì sai mật khẩu nhiều lần", user.Id, opt.LockMinutes);
                throw LockedOut(user.LockoutUntil.Value);
            }

            await db.SaveChangesAsync(ct);
            throw new AppException(401, WrongCredentialsMessage);
        }

        EnsureCanLogin(user);

        var channel = string.Equals(request.Channel, "Sms", StringComparison.OrdinalIgnoreCase) ? OtpChannel.Sms : OtpChannel.Email;
        if (channel == OtpChannel.Sms && (!user.PhoneVerified || string.IsNullOrEmpty(user.PhoneNumber)))
            throw new AppException(400, "Số điện thoại chưa xác thực, vui lòng chọn Email");
        if (channel == OtpChannel.Email && !user.EmailVerified)
            throw new AppException(400, "Email chưa xác thực, vui lòng chọn SMS");

        var destination = channel == OtpChannel.Email ? user.Email : user.PhoneNumber!;
        var sessionId = await otpService.CreateSessionAsync(user.Id, OtpPurpose.Login, channel, destination, ct);
        var sent = await otpService.SendAsync(sessionId, ct);

        return new LoginOtpResponse(user.Username, channel.ToString(), OtpService.Mask(channel, destination),
            sent.ExpiresAt, sent.ResendAfterSeconds);
    }

    public async Task<ResendOtpResponse> ResendLoginOtpAsync(string username, CancellationToken ct = default)
    {
        var sessionId = await FindSessionAsync(username, OtpPurpose.Login, ct);
        var sent = await otpService.SendAsync(sessionId, ct);
        return new ResendOtpResponse(sent.ExpiresAt, sent.ResendAfterSeconds);
    }

    public async Task<AuthResponse> VerifyLoginAsync(LoginVerifyRequest request, CancellationToken ct = default)
    {
        var sessionId = await FindSessionAsync(request.Username, OtpPurpose.Login, ct);
        var result = await otpService.VerifyAsync(sessionId, request.Code, ct);

        var user = await db.Users
                       .Include(u => u.Role)
                       .Include(u => u.Department)
                       .FirstOrDefaultAsync(u => u.Id == result.UserId, ct)
                   ?? throw new AppException(404, "Không tìm thấy tài khoản");

        // Trạng thái có thể đổi giữa 2 bước
        if (user.LockoutUntil > DateTime.UtcNow) throw LockedOut(user.LockoutUntil.Value);
        EnsureCanLogin(user);

        user.FailedLoginCount = 0;
        user.LockoutUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} ({Username}) đăng nhập qua OTP {Channel}", user.Id, user.Username, result.Channel);
        return await tokenService.IssueAsync(user, request.DeviceId?.Trim(), ct);
    }

    public Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default) =>
        tokenService.RefreshAsync(refreshToken, ct);

    public Task LogoutAsync(int userId, string refreshToken, CancellationToken ct = default) =>
        tokenService.RevokeAsync(userId, refreshToken, ct);

    public async Task<ForgotPasswordResponse> ForgotPasswordAsync(string emailOrPhone, CancellationToken ct = default)
    {
        const string message = "Nếu tài khoản tồn tại, mã đã được gửi";
        var input = emailOrPhone.Trim();

        User? user;
        OtpChannel channel;
        string destination;
        if (input.Contains('@'))
        {
            destination = input.ToLowerInvariant();
            channel = OtpChannel.Email;
            user = await db.Users.FirstOrDefaultAsync(u => u.Email == destination && u.EmailVerified, ct);
        }
        else if (PhoneHelper.TryNormalize(input, out var phone))
        {
            destination = phone;
            channel = OtpChannel.Sms;
            user = await db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone && u.PhoneVerified, ct);
        }
        else
        {
            return new ForgotPasswordResponse(message, null);
        }

        if (user is null || user.Status == UserStatus.Locked)
            return new ForgotPasswordResponse(message, null);

        var sessionId = await otpService.CreateSessionAsync(user.Id, OtpPurpose.ResetPassword, channel, destination, ct);
        await otpService.SendAsync(sessionId, ct);
        return new ForgotPasswordResponse(message, user.Username);
    }

    public async Task<MessageResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var sessionId = await FindSessionAsync(request.Username, OtpPurpose.ResetPassword, ct);
        var result = await otpService.VerifyAsync(sessionId, request.Code, ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == result.UserId, ct)
                   ?? throw new AppException(404, "Không tìm thấy tài khoản");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.FailedLoginCount = 0;
        user.LockoutUntil = null;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tokenService.RevokeAllAsync(user.Id, null, ct);

        logger.LogInformation("User {UserId} đặt lại mật khẩu bằng OTP", user.Id);
        return new MessageResponse("Đặt lại mật khẩu thành công, vui lòng đăng nhập lại");
    }

    private const string WrongCredentialsMessage = "Tên đăng nhập hoặc mật khẩu không đúng";

    internal static AppException LockedOut(DateTime lockoutUntil)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling((lockoutUntil - DateTime.UtcNow).TotalSeconds));
        var minutes = (int)Math.Ceiling(seconds / 60.0);
        return new AppException(423, $"Tài khoản tạm khóa, thử lại sau {minutes} phút",
            new Dictionary<string, object?> { ["retryAfterSeconds"] = seconds });
    }

    internal static void EnsureCanLogin(User user)
    {
        if (user.Status == UserStatus.Locked)
            throw new AppException(403, "Tài khoản đã bị khóa");
        if (!user.EmailVerified && !user.PhoneVerified)
            throw new AppException(403, "Tài khoản chưa xác thực");
        if (user.Status == UserStatus.PendingApproval)
            throw new AppException(403, "Tài khoản đang chờ quản trị viên duyệt");
    }

    /// <summary>Phiên OTP mới nhất còn hiệu lực của tài khoản theo tên đăng nhập và mục đích.</summary>
    private async Task<Guid> FindSessionAsync(string username, OtpPurpose purpose, CancellationToken ct)
    {
        username = username.Trim();
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Username == username)
            .Select(u => new { u.Id, u.EmailVerified, u.PhoneVerified })
            .FirstOrDefaultAsync(ct)
            ?? throw new AppException(404, "Không tìm thấy tài khoản");

        var sessionId = await db.OtpCodes
            .Where(o => o.UserId == user.Id && o.Purpose == purpose && !o.Used)
            .OrderByDescending(o => o.Id)
            .Select(o => (Guid?)o.SessionId)
            .FirstOrDefaultAsync(ct);

        if (sessionId is not null) return sessionId.Value;

        throw new AppException(400, purpose switch
        {
            OtpPurpose.RegisterVerify when user.EmailVerified || user.PhoneVerified => "Tài khoản đã được xác thực",
            OtpPurpose.RegisterVerify => "Không có mã xác thực đang chờ cho tài khoản này, vui lòng đăng ký lại",
            OtpPurpose.Login => "Không có mã đăng nhập đang chờ, vui lòng đăng nhập lại",
            _ => "Không có mã đặt lại mật khẩu đang chờ, vui lòng yêu cầu lại"
        });
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}

