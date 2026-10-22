using Microsoft.EntityFrameworkCore;
using NguyenHoaiPhong_1681.Authorization;
using NguyenHoaiPhong_1681.Data;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Entities;
using NguyenHoaiPhong_1681.Middleware;

namespace NguyenHoaiPhong_1681.Services;

public interface IRoleAdminService
{
    Task<IReadOnlyList<PermissionDto>> ListPermissionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken ct = default);
    Task<RoleDto> CreateAsync(CreateRoleRequest request, CancellationToken ct = default);
    Task<RoleDto> UpdateAsync(int id, UpdateRoleRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<RoleDto> SetPermissionsAsync(int id, IReadOnlyCollection<string> permissionCodes, CancellationToken ct = default);
}

public class RoleAdminService(AppDbContext db, IPermissionService permissionService) : IRoleAdminService
{
    public async Task<IReadOnlyList<PermissionDto>> ListPermissionsAsync(CancellationToken ct = default) =>
        await db.Permissions.AsNoTracking()
            .OrderBy(p => p.Group).ThenBy(p => p.Code)
            .Select(p => new PermissionDto(p.Id, p.Code, p.Name, p.Group))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken ct = default)
    {
        var roles = await db.Roles.AsNoTracking().OrderBy(r => r.Id).ToListAsync(ct);
        var result = new List<RoleDto>(roles.Count);
        foreach (var role in roles) result.Add(await ToDtoAsync(role, ct));
        return result;
    }

    public async Task<RoleDto> CreateAsync(CreateRoleRequest request, CancellationToken ct = default)
    {
        var code = request.Code.Trim();
        if (await db.Roles.AnyAsync(r => r.Code == code, ct))
            throw new AppException(409, "Mã vai trò đã tồn tại");

        var role = new Role { Code = code, Name = request.Name.Trim(), Description = request.Description?.Trim(), IsSystem = false };
        db.Roles.Add(role);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(role, ct);
    }

    public async Task<RoleDto> UpdateAsync(int id, UpdateRoleRequest request, CancellationToken ct = default)
    {
        var role = await LoadAsync(id, ct);
        role.Name = request.Name.Trim();
        role.Description = request.Description?.Trim();
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(role, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var role = await LoadAsync(id, ct);
        if (role.IsSystem)
            throw new AppException(400, "Không thể xóa vai trò hệ thống");
        // Tính cả user đã xóa mềm vì vẫn còn khóa ngoại
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.RoleId == id, ct))
            throw new AppException(409, "Vai trò còn người dùng, hãy chuyển họ sang vai trò khác trước");

        db.Roles.Remove(role);
        await db.SaveChangesAsync(ct);
        permissionService.Invalidate(id);
    }

    public async Task<RoleDto> SetPermissionsAsync(int id, IReadOnlyCollection<string> permissionCodes, CancellationToken ct = default)
    {
        var role = await LoadAsync(id, ct);
        var codes = permissionCodes.Select(c => c.Trim()).Where(c => c.Length > 0).Distinct().ToList();

        var permissions = await db.Permissions.Where(p => codes.Contains(p.Code)).ToListAsync(ct);
        var unknown = codes.Except(permissions.Select(p => p.Code)).ToList();
        if (unknown.Count > 0)
            throw new AppException(400, $"Mã quyền không tồn tại: {string.Join(", ", unknown)}");
        if (role.Code == Role.Admin && !codes.Contains(PermissionCodes.RolesManage))
            throw new AppException(400, $"Không thể bỏ quyền {PermissionCodes.RolesManage} khỏi Admin");

        var current = await db.RolePermissions.Where(rp => rp.RoleId == id).ToListAsync(ct);
        var wantedIds = permissions.Select(p => p.Id).ToHashSet();
        db.RolePermissions.RemoveRange(current.Where(rp => !wantedIds.Contains(rp.PermissionId)));
        foreach (var permissionId in wantedIds.Except(current.Select(rp => rp.PermissionId)))
            db.RolePermissions.Add(new RolePermission { RoleId = id, PermissionId = permissionId });

        await db.SaveChangesAsync(ct);
        permissionService.Invalidate(id);
        return await ToDtoAsync(role, ct);
    }

    private async Task<Role> LoadAsync(int id, CancellationToken ct) =>
        await db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new AppException(404, "Không tìm thấy vai trò");

    private async Task<RoleDto> ToDtoAsync(Role role, CancellationToken ct)
    {
        var permissions = await db.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => rp.Permission.Code)
            .OrderBy(c => c)
            .ToListAsync(ct);
        var userCount = await db.Users.CountAsync(u => u.RoleId == role.Id, ct);
        return new RoleDto(role.Id, role.Code, role.Name, role.Description, role.IsSystem, userCount, permissions);
    }
}
