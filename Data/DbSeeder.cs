using Microsoft.EntityFrameworkCore;
using NguyenHoaiPhong_1681.Authorization;
using NguyenHoaiPhong_1681.Entities;
using NguyenHoaiPhong_1681.Enums;

namespace NguyenHoaiPhong_1681.Data;

/// <summary>Seed dữ liệu khi khởi động (idempotent: chỉ thêm cái còn thiếu, không ghi đè chỉnh sửa của Admin).</summary>
public static class DbSeeder
{
    private const string AdminUsername = "admin";
    private const string AdminPassword = "Admin@1681";
    private const string AdminEmail = "hoaiphongdz1312@gmail.com";

    public static async Task SeedAsync(AppDbContext db, IHostEnvironment env, ILogger logger, CancellationToken ct = default)
    {
        await RemoveLegacyCustomerAsync(db, logger, ct);
        var roles = await EnsureRolesAsync(db, ct);
        await EnsurePermissionsAsync(db, roles, ct);
        await EnsureAdminAsync(db, roles[Role.Admin], logger, ct);

        if (env.IsDevelopment())
            await SeedDevelopmentDataAsync(db, roles, ct);
    }

    /// <summary>Role Customer đã bỏ: chuyển user cũ sang Employee rồi xóa role và quyền dashboard.customer.</summary>
    private static async Task RemoveLegacyCustomerAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var customerId = await db.Roles.Where(r => r.Code == "Customer").Select(r => (int?)r.Id).FirstOrDefaultAsync(ct);
        if (customerId is { } oldId)
        {
            var employeeId = await db.Roles.Where(r => r.Code == Role.Employee).Select(r => (int?)r.Id).FirstOrDefaultAsync(ct);
            if (employeeId is { } newId)
            {
                var moved = await db.Users.IgnoreQueryFilters().Where(u => u.RoleId == oldId)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.RoleId, newId), ct);
                await db.RolePermissions.Where(rp => rp.RoleId == oldId).ExecuteDeleteAsync(ct);
                await db.Roles.Where(r => r.Id == oldId).ExecuteDeleteAsync(ct);
                logger.LogInformation("Đã bỏ role Customer, chuyển {Count} user sang Employee", moved);
            }
        }

        var permissionId = await db.Permissions.Where(p => p.Code == "dashboard.customer").Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (permissionId is { } pid)
        {
            await db.RolePermissions.Where(rp => rp.PermissionId == pid).ExecuteDeleteAsync(ct);
            await db.Permissions.Where(p => p.Id == pid).ExecuteDeleteAsync(ct);
        }
    }

    private static async Task<Dictionary<string, Role>> EnsureRolesAsync(AppDbContext db, CancellationToken ct)
    {
        (string Code, string Name, string Description)[] systemRoles =
        [
            (Role.Admin, "Quản trị viên", "Toàn quyền hệ thống"),
            (Role.Manager, "Quản lý", "Quản lý phòng ban, duyệt chấm công và nghỉ phép"),
            (Role.Employee, "Nhân viên", "Chấm công, xin nghỉ phép")
        ];

        var existing = await db.Roles.ToDictionaryAsync(r => r.Code, ct);
        foreach (var (code, name, description) in systemRoles.Where(r => !existing.ContainsKey(r.Code)))
        {
            var role = new Role { Code = code, Name = name, Description = description, IsSystem = true };
            db.Roles.Add(role);
            existing[code] = role;
        }
        await db.SaveChangesAsync(ct);
        return existing;
    }

    private static async Task EnsurePermissionsAsync(AppDbContext db, Dictionary<string, Role> roles, CancellationToken ct)
    {
        var existing = await db.Permissions.ToDictionaryAsync(p => p.Code, ct);
        var added = new List<Permission>();
        foreach (var def in PermissionCodes.All.Where(d => !existing.ContainsKey(d.Code)))
        {
            var permission = new Permission { Code = def.Code, Name = def.Name, Group = def.Group };
            db.Permissions.Add(permission);
            existing[def.Code] = permission;
            added.Add(permission);
        }
        await db.SaveChangesAsync(ct);

        // Quyền mới thêm → gán theo mặc định; quyền cũ giữ nguyên chỉnh sửa của Admin
        foreach (var permission in added)
        {
            foreach (var (roleCode, codes) in PermissionCodes.Defaults)
            {
                if (codes.Contains(permission.Code) && roles.TryGetValue(roleCode, out var role))
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            }
        }

        // Admin luôn đủ mọi quyền
        var adminId = roles[Role.Admin].Id;
        var adminHas = await db.RolePermissions.Where(rp => rp.RoleId == adminId).Select(rp => rp.PermissionId).ToListAsync(ct);
        foreach (var permission in existing.Values.Where(p => !adminHas.Contains(p.Id)))
            db.RolePermissions.Add(new RolePermission { RoleId = adminId, PermissionId = permission.Id });

        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureAdminAsync(AppDbContext db, Role adminRole, ILogger logger, CancellationToken ct)
    {
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == AdminUsername, ct)) return;

        var email = AdminEmail;
        var holder = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email, ct);
        if (holder is not null)
        {
            if (!holder.EmailVerified && !holder.PhoneVerified && holder.Status == UserStatus.PendingApproval)
            {
                // Bản đăng ký bỏ dở (chưa xác thực) đang giữ email của admin → xóa để admin dùng email này
                logger.LogWarning("Seed: xóa tài khoản chưa xác thực {Username} đang giữ email {Email} để tạo admin", holder.Username, email);
                db.Users.Remove(holder);
                await db.SaveChangesAsync(ct);
            }
            else
            {
                email = "admin@example.com";
                logger.LogWarning("Seed: email {Email} đã thuộc tài khoản {Username}, admin dùng {Fallback}", AdminEmail, holder.Username, email);
            }
        }

        db.Users.Add(NewUser(AdminUsername, AdminPassword, "Quản trị viên", adminRole.Id, email));
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: đã tạo tài khoản admin");
    }

    private static async Task SeedDevelopmentDataAsync(AppDbContext db, Dictionary<string, Role> roles, CancellationToken ct)
    {
        var tech = await EnsureDepartmentAsync(db, "Phòng Kỹ thuật", "Phát triển và vận hành hệ thống", ct);
        await EnsureDepartmentAsync(db, "Phòng Kinh doanh", "Bán hàng và chăm sóc khách hàng", ct);

        await EnsureShiftAsync(db, "Ca sáng", new TimeOnly(8, 0), new TimeOnly(12, 0), ct);
        await EnsureShiftAsync(db, "Ca chiều", new TimeOnly(13, 0), new TimeOnly(17, 0), ct);
        await EnsureShiftAsync(db, "Hành chính", new TimeOnly(8, 0), new TimeOnly(17, 0), ct);

        var manager = await EnsureUserAsync(db, "quanly", "Manager@123", "Trần Quản Lý", roles[Role.Manager].Id, tech.Id, ct);
        await EnsureUserAsync(db, "nhanvien1", "Employee@123", "Nguyễn Văn Một", roles[Role.Employee].Id, tech.Id, ct);
        await EnsureUserAsync(db, "nhanvien2", "Employee@123", "Lê Thị Hai", roles[Role.Employee].Id, tech.Id, ct);

        if (tech.ManagerId is null)
        {
            tech.ManagerId = manager.Id;
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task<Department> EnsureDepartmentAsync(AppDbContext db, string name, string description, CancellationToken ct)
    {
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Name == name && !d.IsDeleted, ct);
        if (department is not null) return department;

        department = new Department { Name = name, Description = description, CreatedAt = DateTime.UtcNow };
        db.Departments.Add(department);
        await db.SaveChangesAsync(ct);
        return department;
    }

    private static async Task EnsureShiftAsync(AppDbContext db, string name, TimeOnly start, TimeOnly end, CancellationToken ct)
    {
        if (await db.Shifts.AnyAsync(s => s.Name == name && !s.IsDeleted, ct)) return;

        db.Shifts.Add(new Shift
        {
            Name = name,
            StartTime = start,
            EndTime = end,
            LateToleranceMinutes = 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task<User> EnsureUserAsync(
        AppDbContext db, string username, string password, string fullName, int roleId, int? departmentId, CancellationToken ct)
    {
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Username == username, ct);
        if (user is not null) return user;

        user = NewUser(username, password, fullName, roleId, $"{username}@example.com");
        user.DepartmentId = departmentId;
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }

    private static User NewUser(string username, string password, string fullName, int roleId, string email) => new()
    {
        Username = username,
        Email = email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        FullName = fullName,
        RoleId = roleId,
        Status = UserStatus.Active,
        EmailVerified = true,
        CreatedAt = DateTime.UtcNow
    };
}
