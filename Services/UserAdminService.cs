using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NguyenHoaiPhong_1681.Data;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Entities;
using NguyenHoaiPhong_1681.Enums;
using NguyenHoaiPhong_1681.Helpers;
using NguyenHoaiPhong_1681.Middleware;

namespace NguyenHoaiPhong_1681.Services;

public interface IUserAdminService
{
    Task<PagedResult<AdminUserDto>> ListAsync(UserListQuery query, CancellationToken ct = default);
    Task<AdminUserDto> GetAsync(int id, CancellationToken ct = default);
    Task<AdminUserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<AdminUserDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct = default);
    Task<AdminUserDto> ApproveAsync(int id, CancellationToken ct = default);
    Task<AdminUserDto> LockAsync(int id, CancellationToken ct = default);
    Task<AdminUserDto> UnlockAsync(int id, CancellationToken ct = default);
    Task<AdminUserDto> ChangeRoleAsync(int id, string roleCode, CancellationToken ct = default);
    Task ResetPasswordAsync(int id, string newPassword, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class UserAdminService(AppDbContext db, ICurrentUser currentUser, ITokenService tokenService) : IUserAdminService
{
    private static readonly Expression<Func<User, AdminUserDto>> ToDto = u => new AdminUserDto(
        u.Id, u.Username, u.FullName, u.Email, u.EmailVerified, u.PhoneNumber, u.PhoneVerified, u.AvatarUrl,
        u.Role.Code, u.Status.ToString(), u.DepartmentId, u.Department != null ? u.Department.Name : null,
        u.LockoutUntil, u.LastLoginAt, u.CreatedAt);

    public async Task<PagedResult<AdminUserDto>> ListAsync(UserListQuery query, CancellationToken ct = default)
    {
        var q = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(u => u.Username.Contains(s) || u.FullName.Contains(s) || u.Email.Contains(s)
                             || (u.PhoneNumber != null && u.PhoneNumber.Contains(s)));
        }
        if (!string.IsNullOrWhiteSpace(query.RoleCode))
            q = q.Where(u => u.Role.Code == query.RoleCode);
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<UserStatus>(query.Status, true, out var status))
                throw new AppException(400, "Trạng thái chỉ nhận PendingApproval, Active, Locked");
            q = q.Where(u => u.Status == status);
        }
        if (query.DepartmentId is { } departmentId)
            q = q.Where(u => u.DepartmentId == departmentId);

        return await q.OrderByDescending(u => u.CreatedAt).Select(ToDto).ToPagedAsync(query, ct);
    }

    public async Task<AdminUserDto> GetAsync(int id, CancellationToken ct = default) =>
        await db.Users.AsNoTracking().Where(u => u.Id == id).Select(ToDto).FirstOrDefaultAsync(ct)
        ?? throw NotFound();

    public async Task<AdminUserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var phone = NormalizePhone(request.Phone);

        // Unique index gồm cả user đã xóa mềm
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == username, ct))
            throw new AppException(409, "Tên đăng nhập đã tồn tại");
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
            throw new AppException(409, "Email đã được sử dụng");

        var role = await FindRoleAsync(request.RoleCode, ct);
        await EnsureDepartmentAsync(request.DepartmentId, ct);

        var user = new User
        {
            Username = username,
            Email = email,
            EmailVerified = true,
            PhoneNumber = phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = request.FullName.Trim(),
            RoleId = role.Id,
            DepartmentId = request.DepartmentId,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return await GetAsync(user.Id, ct);
    }

    public async Task<AdminUserDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await LoadAsync(id, ct);
        var email = request.Email.Trim().ToLowerInvariant();
        if (email != user.Email && await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email && u.Id != id, ct))
            throw new AppException(409, "Email đã được sử dụng");

        var phone = NormalizePhone(request.Phone);
        if (phone != user.PhoneNumber)
        {
            user.PhoneNumber = phone;
            user.PhoneVerified = false;
        }

        await EnsureDepartmentAsync(request.DepartmentId, ct);
        user.Email = email;
        user.FullName = request.FullName.Trim();
        user.DepartmentId = request.DepartmentId;
        user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdminUserDto> ApproveAsync(int id, CancellationToken ct = default)
    {
        var user = await LoadAsync(id, ct);
        if (user.Status != UserStatus.PendingApproval)
            throw new AppException(400, "Tài khoản không ở trạng thái chờ duyệt");

        user.Status = UserStatus.Active;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdminUserDto> LockAsync(int id, CancellationToken ct = default)
    {
        var user = await LoadAsync(id, ct);
        EnsureNotSelf(id, "khóa");
        await EnsureNotLastAdminAsync(user, "khóa", ct);

        user.Status = UserStatus.Locked;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tokenService.RevokeAllAsync(id, null, ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdminUserDto> UnlockAsync(int id, CancellationToken ct = default)
    {
        var user = await LoadAsync(id, ct);
        if (user.Status == UserStatus.Locked) user.Status = UserStatus.Active;
        user.FailedLoginCount = 0;
        user.LockoutUntil = null;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdminUserDto> ChangeRoleAsync(int id, string roleCode, CancellationToken ct = default)
    {
        var user = await LoadAsync(id, ct);
        var role = await FindRoleAsync(roleCode, ct);
        if (role.Id == user.RoleId) return await GetAsync(id, ct);

        EnsureNotSelf(id, "đổi vai trò của");
        if (role.Code != Role.Admin) await EnsureNotLastAdminAsync(user, "hạ quyền", ct);

        // Bỏ role Manager → gỡ khỏi vị trí quản lý phòng ban
        if (user.Role.Code == Role.Manager && role.Code != Role.Manager)
        {
            await db.Departments.Where(d => d.ManagerId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.ManagerId, (int?)null), ct);
        }

        user.RoleId = role.Id;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task ResetPasswordAsync(int id, string newPassword, CancellationToken ct = default)
    {
        var user = await LoadAsync(id, ct);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.FailedLoginCount = 0;
        user.LockoutUntil = null;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tokenService.RevokeAllAsync(id, null, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var user = await LoadAsync(id, ct);
        EnsureNotSelf(id, "xóa");
        await EnsureNotLastAdminAsync(user, "xóa", ct);

        await db.Departments.Where(d => d.ManagerId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.ManagerId, (int?)null), ct);
        user.IsDeleted = true;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tokenService.RevokeAllAsync(id, null, ct);
    }

    private async Task<User> LoadAsync(int id, CancellationToken ct) =>
        await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw NotFound();

    private async Task<Role> FindRoleAsync(string roleCode, CancellationToken ct) =>
        await db.Roles.FirstOrDefaultAsync(r => r.Code == roleCode.Trim(), ct)
        ?? throw new AppException(400, $"Vai trò \"{roleCode}\" không tồn tại");

    private async Task EnsureDepartmentAsync(int? departmentId, CancellationToken ct)
    {
        if (departmentId is { } id && !await db.Departments.AnyAsync(d => d.Id == id && !d.IsDeleted, ct))
            throw new AppException(400, "Phòng ban không tồn tại");
    }

    private void EnsureNotSelf(int targetId, string action)
    {
        if (targetId == currentUser.UserId)
            throw new AppException(400, $"Không thể tự {action} chính mình");
    }

    private async Task EnsureNotLastAdminAsync(User target, string action, CancellationToken ct)
    {
        if (target.Role.Code != Role.Admin) return;
        var otherAdmins = await db.Users.CountAsync(u =>
            u.Id != target.Id && u.Role.Code == Role.Admin && u.Status == UserStatus.Active, ct);
        if (otherAdmins == 0)
            throw new AppException(409, $"Không thể {action} Admin cuối cùng của hệ thống");
    }

    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        return PhoneHelper.TryNormalize(phone, out var normalized)
            ? normalized
            : throw new AppException(400, "Số điện thoại không hợp lệ (di động Việt Nam 10 số đầu 03/05/07/08/09, hoặc +84/84)");
    }

    private static AppException NotFound() => new(404, "Không tìm thấy người dùng");
}
