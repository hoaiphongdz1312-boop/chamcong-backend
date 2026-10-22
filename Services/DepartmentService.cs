using System.Linq.Expressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NguyenHoaiPhong_1681.Authorization;
using NguyenHoaiPhong_1681.Data;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Entities;
using NguyenHoaiPhong_1681.Middleware;

namespace NguyenHoaiPhong_1681.Services;

public interface IDepartmentService
{
    Task<PagedResult<DepartmentDto>> ListAsync(DepartmentListQuery query, CancellationToken ct = default);
    Task<DepartmentDto> GetAsync(int id, CancellationToken ct = default);
    Task<DepartmentDto> CreateAsync(SaveDepartmentRequest request, CancellationToken ct = default);
    Task<DepartmentDto> UpdateAsync(int id, SaveDepartmentRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<DepartmentDto> SetManagerAsync(int id, int userId, CancellationToken ct = default);
    Task<IReadOnlyList<DepartmentMemberDto>> GetMembersAsync(int id, CancellationToken ct = default);
}

public class DepartmentService(AppDbContext db, ICurrentUser currentUser) : IDepartmentService
{
    private static readonly Expression<Func<Department, DepartmentDto>> ToDto = d => new DepartmentDto(
        d.Id, d.Name, d.Description, d.ManagerId, d.Manager != null ? d.Manager.FullName : null,
        d.Members.Count(u => !u.IsDeleted), d.CreatedAt);

    public async Task<PagedResult<DepartmentDto>> ListAsync(DepartmentListQuery query, CancellationToken ct = default)
    {
        var q = await ScopedQueryAsync(ct);
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(d => d.Name.Contains(query.Search.Trim()));
        return await q.OrderBy(d => d.Name).Select(ToDto).ToPagedAsync(query, ct);
    }

    public async Task<DepartmentDto> GetAsync(int id, CancellationToken ct = default) =>
        await (await ScopedQueryAsync(ct)).Where(d => d.Id == id).Select(ToDto).FirstOrDefaultAsync(ct)
        ?? throw NotFound();

    public async Task<DepartmentDto> CreateAsync(SaveDepartmentRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        await EnsureNameFreeAsync(name, null, ct);
        await EnsureManagerAsync(request.ManagerId, ct);

        var department = new Department
        {
            Name = name,
            Description = request.Description?.Trim(),
            ManagerId = request.ManagerId,
            CreatedAt = DateTime.UtcNow
        };
        db.Departments.Add(department);
        await SaveAsync(ct);
        return await GetAsync(department.Id, ct);
    }

    public async Task<DepartmentDto> UpdateAsync(int id, SaveDepartmentRequest request, CancellationToken ct = default)
    {
        var department = await LoadAsync(id, ct);
        var name = request.Name.Trim();
        await EnsureNameFreeAsync(name, id, ct);
        await EnsureManagerAsync(request.ManagerId, ct);

        department.Name = name;
        department.Description = request.Description?.Trim();
        department.ManagerId = request.ManagerId;
        await SaveAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var department = await LoadAsync(id, ct);
        if (await db.Users.AnyAsync(u => u.DepartmentId == id, ct))
            throw new AppException(409, "Phòng ban còn nhân viên, hãy chuyển nhân viên sang phòng khác trước");

        department.IsDeleted = true;
        department.ManagerId = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task<DepartmentDto> SetManagerAsync(int id, int userId, CancellationToken ct = default)
    {
        var department = await LoadAsync(id, ct);
        await EnsureManagerAsync(userId, ct);
        department.ManagerId = userId;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<IReadOnlyList<DepartmentMemberDto>> GetMembersAsync(int id, CancellationToken ct = default)
    {
        await LoadAsync(id, ct);
        if (!await currentUser.HasPermissionAsync(PermissionCodes.DepartmentsManage, ct)
            && !(await currentUser.GetManagedDepartmentIdsAsync(ct)).Contains(id))
            throw new AppException(403, "Bạn không quản lý phòng ban này");

        return await db.Users.AsNoTracking()
            .Where(u => u.DepartmentId == id)
            .OrderBy(u => u.FullName)
            .Select(u => new DepartmentMemberDto(u.Id, u.Username, u.FullName, u.Email, u.PhoneNumber, u.Role.Code, u.Status.ToString()))
            .ToListAsync(ct);
    }

    /// <summary>Có departments.manage → mọi phòng; chỉ departments.view → phòng mình quản lý hoặc trực thuộc.</summary>
    private async Task<IQueryable<Department>> ScopedQueryAsync(CancellationToken ct)
    {
        var q = db.Departments.AsNoTracking().Where(d => !d.IsDeleted);
        if (await currentUser.HasPermissionAsync(PermissionCodes.DepartmentsManage, ct)) return q;

        var me = await currentUser.GetAsync(ct);
        var managed = await currentUser.GetManagedDepartmentIdsAsync(ct);
        return q.Where(d => managed.Contains(d.Id) || d.Id == me.DepartmentId);
    }

    private async Task<Department> LoadAsync(int id, CancellationToken ct) =>
        await db.Departments.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct) ?? throw NotFound();

    private async Task EnsureNameFreeAsync(string name, int? exceptId, CancellationToken ct)
    {
        if (await db.Departments.AnyAsync(d => d.Name == name && !d.IsDeleted && d.Id != exceptId, ct))
            throw new AppException(409, "Tên phòng ban đã tồn tại");
    }

    private async Task EnsureManagerAsync(int? userId, CancellationToken ct)
    {
        if (userId is null) return;
        var roleCode = await db.Users.Where(u => u.Id == userId).Select(u => u.Role.Code).FirstOrDefaultAsync(ct)
                       ?? throw new AppException(400, "Người quản lý không tồn tại");
        if (roleCode != Role.Manager)
            throw new AppException(400, "Người quản lý phải có vai trò Manager");
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new AppException(409, "Tên phòng ban đã tồn tại");
        }
    }

    private static AppException NotFound() => new(404, "Không tìm thấy phòng ban");
}
