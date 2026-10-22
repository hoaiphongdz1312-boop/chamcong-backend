using Microsoft.EntityFrameworkCore;
using NguyenHoaiPhong_1681.Data;
using NguyenHoaiPhong_1681.Entities;
using NguyenHoaiPhong_1681.Enums;
using NguyenHoaiPhong_1681.Helpers;
using NguyenHoaiPhong_1681.Middleware;

namespace NguyenHoaiPhong_1681.Services;

public record CurrentUserInfo(
    int Id,
    string Username,
    int RoleId,
    string RoleCode,
    int? DepartmentId,
    UserStatus Status,
    DateTime? LockoutUntil)
{
    public bool IsAdmin => RoleCode == Role.Admin;

    /// <summary>Đang hoạt động và không bị khóa tạm.</summary>
    public bool CanAct => Status == UserStatus.Active && (LockoutUntil is null || LockoutUntil <= DateTime.UtcNow);
}

public interface ICurrentUser
{
    int UserId { get; }

    /// <summary>Thông tin user ĐỌC TỪ DB (không tin claim). Không còn tồn tại → 401.</summary>
    Task<CurrentUserInfo> GetAsync(CancellationToken ct = default);

    Task<bool> HasPermissionAsync(string permissionCode, CancellationToken ct = default);

    /// <summary>Các phòng ban user đang làm quản lý.</summary>
    Task<IReadOnlyList<int>> GetManagedDepartmentIdsAsync(CancellationToken ct = default);
}

/// <summary>User của request hiện tại; cache trong phạm vi 1 request.</summary>
public class CurrentUserService(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    IPermissionService permissionService) : ICurrentUser
{
    private CurrentUserInfo? _user;
    private IReadOnlyList<int>? _managedDepartments;

    public int UserId => httpContextAccessor.HttpContext?.User.GetUserId()
                         ?? throw new AppException(401, "Bạn chưa đăng nhập hoặc phiên đã hết hạn");

    public async Task<CurrentUserInfo> GetAsync(CancellationToken ct = default)
    {
        if (_user is not null) return _user;

        var userId = UserId;
        _user = await db.Users.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new CurrentUserInfo(u.Id, u.Username, u.RoleId, u.Role.Code, u.DepartmentId, u.Status, u.LockoutUntil))
                    .FirstOrDefaultAsync(ct)
                ?? throw new AppException(401, "Tài khoản không còn tồn tại");
        return _user;
    }

    public async Task<bool> HasPermissionAsync(string permissionCode, CancellationToken ct = default)
    {
        var user = await GetAsync(ct);
        if (!user.CanAct) return false;
        if (user.IsAdmin) return true;
        var perms = await permissionService.GetRolePermissionsAsync(user.RoleId, ct);
        return perms.Contains(permissionCode);
    }

    public async Task<IReadOnlyList<int>> GetManagedDepartmentIdsAsync(CancellationToken ct = default)
    {
        if (_managedDepartments is not null) return _managedDepartments;

        var userId = UserId;
        _managedDepartments = await db.Departments.AsNoTracking()
            .Where(d => d.ManagerId == userId && !d.IsDeleted)
            .Select(d => d.Id)
            .ToListAsync(ct);
        return _managedDepartments;
    }
}

public static class CurrentUserExtensions
{
    public const string ForbiddenMessage = "Bạn không có quyền thực hiện thao tác này";

    /// <summary>
    /// Phạm vi dữ liệu: null = toàn bộ (có quyền allCode); ngược lại danh sách phòng ban được phép (có quyền teamCode).
    /// Không có quyền nào → 403.
    /// </summary>
    public static async Task<IReadOnlyList<int>?> GetDepartmentScopeAsync(
        this ICurrentUser currentUser, string allCode, string teamCode, CancellationToken ct = default)
    {
        if (await currentUser.HasPermissionAsync(allCode, ct)) return null;
        if (await currentUser.HasPermissionAsync(teamCode, ct)) return await currentUser.GetManagedDepartmentIdsAsync(ct);
        throw new AppException(403, ForbiddenMessage);
    }

    public static async Task EnsurePermissionAsync(this ICurrentUser currentUser, string code, CancellationToken ct = default)
    {
        if (!await currentUser.HasPermissionAsync(code, ct))
            throw new AppException(403, ForbiddenMessage);
    }
}
