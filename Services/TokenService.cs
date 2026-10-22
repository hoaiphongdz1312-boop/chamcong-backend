using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NguyenHoaiPhong_1681.Data;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Entities;
using NguyenHoaiPhong_1681.Enums;
using NguyenHoaiPhong_1681.Helpers;
using NguyenHoaiPhong_1681.Middleware;
using NguyenHoaiPhong_1681.Options;

namespace NguyenHoaiPhong_1681.Services;

public interface ITokenService
{
    /// <summary>Cấp access + refresh token mới cho user (đã Include Role, Department).</summary>
    Task<AuthResponse> IssueAsync(User user, string? deviceId, CancellationToken ct = default);

    /// <summary>Đổi refresh token lấy cặp token mới (xoay vòng).</summary>
    Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Thu hồi 1 refresh token của user.</summary>
    Task RevokeAsync(int userId, string refreshToken, CancellationToken ct = default);

    /// <summary>Thu hồi toàn bộ refresh token của user, trừ token (gốc) được giữ lại.</summary>
    Task RevokeAllAsync(int userId, string? exceptRefreshToken = null, CancellationToken ct = default);

    Task<MeResponse> BuildMeAsync(int userId, CancellationToken ct = default);
}

public class TokenService(
    AppDbContext db,
    IPermissionService permissionService,
    IAttendanceService attendanceService,
    IOptions<JwtOptions> jwtOptions,
    ILogger<TokenService> logger) : ITokenService
{
    private const string InvalidRefreshMessage = "Phiên đăng nhập không hợp lệ hoặc đã hết hạn, vui lòng đăng nhập lại";
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<AuthResponse> IssueAsync(User user, string? deviceId, CancellationToken ct = default)
    {
        var (refreshToken, refreshHash) = NewRefreshToken();
        var now = DateTime.UtcNow;
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            DeviceId = deviceId,
            CreatedAt = now,
            ExpiresAt = now.AddDays(_jwt.RefreshDays)
        });
        await db.SaveChangesAsync(ct);

        if (user.Role.Code == Role.Employee)
        {
            try
            {
                await attendanceService.AutoCheckInAsync(user.Id, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Tự check-in khi đăng nhập thất bại cho user {UserId}", user.Id);
            }
        }

        return await BuildAuthResponseAsync(user, refreshToken, ct);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
                     ?? throw new AppException(401, InvalidRefreshMessage);

        if (stored.RevokedAt is not null)
        {
            // Token đã thu hồi bị dùng lại → nghi bị đánh cắp: thu hồi toàn bộ phiên của user
            logger.LogWarning("Phát hiện dùng lại refresh token đã thu hồi của user {UserId}", stored.UserId);
            await RevokeAllAsync(stored.UserId, null, ct);
            throw new AppException(401, InvalidRefreshMessage);
        }

        if (stored.ExpiresAt <= DateTime.UtcNow)
            throw new AppException(401, InvalidRefreshMessage);

        var user = await LoadUserAsync(stored.UserId, ct);
        if (user is null || user.Status != UserStatus.Active || user.LockoutUntil > DateTime.UtcNow)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            throw new AppException(401, InvalidRefreshMessage);
        }

        var (newToken, newHash) = NewRefreshToken();
        var now = DateTime.UtcNow;
        stored.RevokedAt = now;
        stored.ReplacedByTokenHash = newHash;
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newHash,
            DeviceId = stored.DeviceId,
            CreatedAt = now,
            ExpiresAt = now.AddDays(_jwt.RefreshDays)
        });
        await db.SaveChangesAsync(ct);

        return await BuildAuthResponseAsync(user, newToken, ct);
    }

    public async Task RevokeAsync(int userId, string refreshToken, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserId == userId, ct);
        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task RevokeAllAsync(int userId, string? exceptRefreshToken = null, CancellationToken ct = default)
    {
        var exceptHash = string.IsNullOrEmpty(exceptRefreshToken) ? null : Hash(exceptRefreshToken);
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.TokenHash != exceptHash)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
    }

    public async Task<MeResponse> BuildMeAsync(int userId, CancellationToken ct = default)
    {
        var user = await LoadUserAsync(userId, ct) ?? throw new AppException(401, "Tài khoản không còn tồn tại");
        var permissions = await permissionService.GetRolePermissionsAsync(user.RoleId, ct);
        return new MeResponse(ToUserInfo(user), permissions.Order().ToList(), RoleRoutes.HomeRoute(user.Role.Code));
    }

    public static UserInfoDto ToUserInfo(User user) => new(
        user.Id,
        user.Username,
        user.FullName,
        user.Email,
        user.PhoneNumber,
        user.AvatarUrl,
        user.Role.Code,
        user.Status.ToString(),
        user.DepartmentId,
        user.Department?.Name,
        user.EmailVerified,
        user.PhoneVerified);

    private async Task<AuthResponse> BuildAuthResponseAsync(User user, string refreshToken, CancellationToken ct)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwt.AccessMinutes);
        var accessToken = CreateAccessToken(user, expiresAt);
        var permissions = await permissionService.GetRolePermissionsAsync(user.RoleId, ct);

        return new AuthResponse(
            accessToken,
            expiresAt,
            refreshToken,
            ToUserInfo(user),
            permissions.Order().ToList(),
            RoleRoutes.HomeRoute(user.Role.Code));
    }

    private string CreateAccessToken(User user, DateTime expiresAt)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.Role, user.Role.Code)
        };
        if (user.DepartmentId is { } departmentId)
            claims.Add(new Claim(ClaimsExtensions.DepartmentIdClaim, departmentId.ToString()));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = DateTime.UtcNow,
            NotBefore = DateTime.UtcNow,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)), SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private Task<User?> LoadUserAsync(int userId, CancellationToken ct) =>
        db.Users
            .Include(u => u.Role)
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

    private static (string Token, string Hash) NewRefreshToken()
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        return (token, Hash(token));
    }

    public static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
