using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NguyenHoaiPhong_1681.Data;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Entities;
using NguyenHoaiPhong_1681.Enums;
using NguyenHoaiPhong_1681.Middleware;
using NguyenHoaiPhong_1681.Options;

namespace NguyenHoaiPhong_1681.Services;

public class FaceLoginService(
    AppDbContext db,
    IFaceService faceService,
    IOtpService otpService,
    ITokenService tokenService,
    IMemoryCache cache,
    IOptions<FaceOptions> faceOptions,
    ILogger<FaceLoginService> logger) : IFaceLoginService
{
    private const string GenericLoginMessage = "Không thể đăng nhập bằng khuôn mặt";
    private const string ChallengeCachePrefix = "face-challenge:";
    private static readonly string[] Actions = ["turn_left", "turn_right"];
    private static readonly object ChallengeGate = new();

    private readonly FaceOptions _opt = faceOptions.Value;

    private sealed record Challenge(string Username, string? DeviceId, string Action);

    public async Task<FaceEnrollResponse> EnrollAsync(int userId, FaceEnrollForm form, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new AppException(404, "Không tìm thấy tài khoản");
        if (!BCrypt.Net.BCrypt.Verify(form.Password, user.PasswordHash))
            throw new AppException(401, "Mật khẩu không đúng");

        if (form.Images.Count != _opt.EnrollImageCount)
            throw new AppException(400, $"Cần đúng {_opt.EnrollImageCount} ảnh khuôn mặt");

        var embeddings = new List<float[]>();
        foreach (var file in form.Images)
            embeddings.Add(faceService.ExtractFace(await ReadImageAsync(file, ct)).Embedding);

        for (var i = 0; i < embeddings.Count; i++)
        for (var j = i + 1; j < embeddings.Count; j++)
            if (faceService.Cosine(embeddings[i], embeddings[j]) < _opt.SamePersonThreshold)
                throw new AppException(400, "Ba ảnh không cùng một người");

        await db.FaceTemplates.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
        db.FaceTemplates.AddRange(embeddings.Select(e => new FaceTemplate { UserId = userId, Embedding = ToBytes(e) }));
        await db.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} đăng ký khuôn mặt", userId);
        return new FaceEnrollResponse(embeddings.Count);
    }

    public async Task<FaceStatusResponse> StatusAsync(int userId, CancellationToken ct = default)
    {
        var count = await db.FaceTemplates.CountAsync(t => t.UserId == userId, ct);
        return new FaceStatusResponse(count > 0, count);
    }

    public async Task DeleteAsync(int userId, CancellationToken ct = default)
    {
        await db.FaceTemplates.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
        logger.LogInformation("User {UserId} xóa khuôn mặt đã đăng ký", userId);
    }

    public FaceChallengeResponse CreateChallenge(FaceChallengeRequest request)
    {
        // Luôn trả cùng một dạng dù user có tồn tại hay không để không lộ username
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var action = Actions[RandomNumberGenerator.GetInt32(Actions.Length)];
        cache.Set(ChallengeCachePrefix + token,
            new Challenge(NormalizeUsername(request.Username), request.DeviceId?.Trim(), action),
            TimeSpan.FromSeconds(_opt.ChallengeSeconds));
        return new FaceChallengeResponse(token, action, _opt.ChallengeSeconds);
    }

    public async Task<FaceLoginResult> LoginAsync(FaceLoginForm form, string? ip, CancellationToken ct = default)
    {
        var username = form.Username.Trim();
        var deviceId = form.DeviceId?.Trim();

        // Chế độ đơn giản: chỉ 1 ảnh, không challenge, không kiểm quay đầu — chỉ so khớp với mẫu đã đăng ký
        var simple = string.IsNullOrEmpty(form.ChallengeToken) && form.ImageTurn is null;

        // 1. Challenge dùng một lần, xóa ngay kể cả khi thất bại
        Challenge? challenge = null;
        if (!simple)
        {
            lock (ChallengeGate)
            {
                var key = ChallengeCachePrefix + form.ChallengeToken;
                cache.TryGetValue(key, out challenge);
                cache.Remove(key);
            }

            if (challenge is null
                || challenge.Username != NormalizeUsername(username)
                || !string.Equals(challenge.DeviceId ?? "", deviceId ?? "", StringComparison.Ordinal))
            {
                throw await RejectAsync(400, "Phiên quét mặt đã hết hạn, vui lòng thử lại", "BadChallenge", null, username, null, ip, deviceId, ct);
            }
        }

        // 2. Tài khoản đủ điều kiện và đã đăng ký mặt; mọi lý do đều trả thông báo chung
        var email = username.ToLowerInvariant();
        var user = await db.Users
            .Include(u => u.Role)
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Username == username || u.Email == email, ct);

        var templates = user is null
            ? []
            : await db.FaceTemplates.AsNoTracking().Where(t => t.UserId == user.Id).Select(t => t.Embedding).ToListAsync(ct);

        if (user is null || templates.Count == 0)
            throw await RejectAsync(401, GenericLoginMessage, "LowScore", null, username, user?.Id, ip, deviceId, ct);

        var now = DateTime.UtcNow;
        try
        {
            if (user.LockoutUntil > now) throw AuthService.LockedOut(user.LockoutUntil.Value);
            AuthService.EnsureCanLogin(user);
        }
        catch (AppException)
        {
            throw await RejectAsync(401, GenericLoginMessage, "Locked", null, username, user.Id, ip, deviceId, ct);
        }

        // 3. Khóa riêng của quét mặt
        if (user.FaceLockoutUntil > now)
        {
            var seconds = Math.Max(1, (int)Math.Ceiling((user.FaceLockoutUntil.Value - now).TotalSeconds));
            var minutes = (int)Math.Ceiling(seconds / 60.0);
            throw await RejectAsync(423, $"Quét mặt tạm khóa, thử lại sau {minutes} phút", "Locked", null, username, user.Id, ip, deviceId,
                ct, new Dictionary<string, object?> { ["retryAfterSeconds"] = seconds });
        }

        // 4. Phát hiện mặt trong 2 ảnh
        FaceInfo start;
        FaceInfo? turn = null;
        try
        {
            start = faceService.ExtractFace(await ReadImageAsync(form.ImageStart!, ct));
            if (!simple) turn = faceService.ExtractFace(await ReadImageAsync(form.ImageTurn!, ct));
        }
        catch (AppException ex)
        {
            throw await RejectAsync(ex.StatusCode, ex.Message, "NoFace", null, username, user.Id, ip, deviceId, ct);
        }

        // 5. Liveness kiểm ở server theo độ quay đầu giữa hai ảnh (bỏ qua ở chế độ đơn giản)
        if (turn is not null && challenge is not null)
        {
            var turnDelta = turn.Yaw - start.Yaw;
            var turnOk = Math.Abs(turnDelta) >= _opt.MinTurnDelta
                         && (!_opt.CheckTurnDirection || (challenge.Action == "turn_left") == (turnDelta < 0));
            if (Math.Abs(start.Yaw) > _opt.MaxNeutralYaw
                || !turnOk
                || faceService.Cosine(start.Embedding, turn.Embedding) < _opt.SamePersonThreshold)
            {
                RegisterFailure(user, now);
                throw await RejectAsync(400, "Chưa qua kiểm tra người thật", "LivenessFailed", null, username, user.Id, ip, deviceId, ct);
            }
        }

        // 6. So khớp với các mẫu đã lưu
        var score = templates.Max(t => faceService.Cosine(start.Embedding, ToFloats(t)));

        // 7. Quyết định theo ngưỡng
        if (score >= _opt.AcceptThreshold)
        {
            user.FaceLoginFailedCount = 0;
            user.FaceLockoutUntil = null;
            user.LastLoginAt = now;
            await db.SaveChangesAsync(ct);
            await LogAsync("Success", score, username, user.Id, ip, deviceId, ct);
            logger.LogInformation("User {UserId} đăng nhập bằng khuôn mặt", user.Id);
            return new FaceLoginResult(await tokenService.IssueAsync(user, deviceId, ct), null);
        }

        if (score >= _opt.OtpThreshold)
        {
            var channel = user.EmailVerified ? OtpChannel.Email : OtpChannel.Sms;
            var destination = channel == OtpChannel.Email ? user.Email : user.PhoneNumber!;
            var sessionId = await otpService.CreateSessionAsync(user.Id, OtpPurpose.Login, channel, destination, ct);
            var sent = await otpService.SendAsync(sessionId, ct);
            await LogAsync("OtpRequired", score, username, user.Id, ip, deviceId, ct);
            return new FaceLoginResult(null, new LoginOtpResponse(user.Username, channel.ToString(),
                OtpService.Mask(channel, destination), sent.ExpiresAt, sent.ResendAfterSeconds));
        }

        RegisterFailure(user, now);
        throw await RejectAsync(401, "Khuôn mặt không khớp", "LowScore", score, username, user.Id, ip, deviceId, ct);
    }

    private void RegisterFailure(User user, DateTime now)
    {
        user.FaceLoginFailedCount++;
        if (user.FaceLoginFailedCount < _opt.MaxFailures) return;
        user.FaceLoginFailedCount = 0;
        user.FaceLockoutUntil = now.AddMinutes(_opt.LockMinutes);
        logger.LogWarning("Khóa quét mặt user {UserId} {Minutes} phút", user.Id, _opt.LockMinutes);
    }

    /// <summary>Lưu thay đổi đang chờ (đếm lần sai), ghi log rồi trả exception để ném.</summary>
    private async Task<AppException> RejectAsync(int status, string message, string result, float? score, string username,
        int? userId, string? ip, string? deviceId, CancellationToken ct, IDictionary<string, object?>? extensions = null)
    {
        await LogAsync(result, score, username, userId, ip, deviceId, ct);
        return new AppException(status, message, extensions);
    }

    private async Task LogAsync(string result, float? score, string username, int? userId, string? ip, string? deviceId, CancellationToken ct)
    {
        db.FaceLoginLogs.Add(new FaceLoginLog
        {
            Result = result,
            Score = score,
            Username = username.Length > 256 ? username[..256] : username,
            UserId = userId,
            Ip = ip,
            DeviceId = deviceId
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task<byte[]> ReadImageAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0) throw new AppException(400, "Ảnh không hợp lệ");
        if (file.Length > _opt.MaxImageBytes)
            throw new AppException(400, $"Ảnh quá lớn, tối đa {_opt.MaxImageBytes / (1024 * 1024.0):0.#} MB mỗi ảnh");
        using var ms = new MemoryStream((int)file.Length);
        await file.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    private static byte[] ToBytes(float[] v) => MemoryMarshal.AsBytes(v.AsSpan()).ToArray();

    private static float[] ToFloats(byte[] b) => MemoryMarshal.Cast<byte, float>(b.AsSpan()).ToArray();
}
