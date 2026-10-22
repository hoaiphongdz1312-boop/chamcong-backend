using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NguyenHoaiPhong_1681.Authorization;
using NguyenHoaiPhong_1681.Data;
using NguyenHoaiPhong_1681.Entities;

namespace NguyenHoaiPhong_1681.Services;

public interface IPermissionService
{
    /// <summary>Mã quyền của role (cache 5 phút). Admin luôn có mọi quyền.</summary>
    Task<IReadOnlySet<string>> GetRolePermissionsAsync(int roleId, CancellationToken ct = default);

    void Invalidate(int roleId);
}

public class PermissionService(AppDbContext db, IMemoryCache cache) : IPermissionService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly IReadOnlySet<string> AllCodes =
        PermissionCodes.All.Select(p => p.Code).ToHashSet();

    public async Task<IReadOnlySet<string>> GetRolePermissionsAsync(int roleId, CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey(roleId), out IReadOnlySet<string>? cached) && cached is not null)
            return cached;

        var roleCode = await db.Roles.Where(r => r.Id == roleId).Select(r => r.Code).FirstOrDefaultAsync(ct);
        IReadOnlySet<string> codes = roleCode == Role.Admin
            ? AllCodes
            : (await db.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .Select(rp => rp.Permission.Code)
                .ToListAsync(ct)).ToHashSet();

        cache.Set(CacheKey(roleId), codes, CacheDuration);
        return codes;
    }

    public void Invalidate(int roleId) => cache.Remove(CacheKey(roleId));

    private static string CacheKey(int roleId) => $"role-perms:{roleId}";
}
