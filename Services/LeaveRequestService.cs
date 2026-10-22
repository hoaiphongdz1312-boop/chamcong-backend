using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NguyenHoaiPhong_1681.Authorization;
using NguyenHoaiPhong_1681.Data;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Entities;
using NguyenHoaiPhong_1681.Enums;
using NguyenHoaiPhong_1681.Middleware;

namespace NguyenHoaiPhong_1681.Services;

public interface ILeaveRequestService
{
    Task<LeaveRequestDto> CreateAsync(SaveLeaveRequest request, CancellationToken ct = default);
    Task<LeaveRequestDto> UpdateAsync(int id, SaveLeaveRequest request, CancellationToken ct = default);
    Task<LeaveRequestDto> CancelAsync(int id, CancellationToken ct = default);
    Task<PagedResult<LeaveRequestDto>> ListMineAsync(MyLeaveQuery query, CancellationToken ct = default);
    Task<PagedResult<LeaveRequestDto>> ListAsync(LeaveQuery query, CancellationToken ct = default);
    Task<LeaveRequestDto> GetAsync(int id, CancellationToken ct = default);
    Task<LeaveRequestDto> ApproveAsync(int id, string? note, CancellationToken ct = default);
    Task<LeaveRequestDto> RejectAsync(int id, string note, CancellationToken ct = default);
}

public class LeaveRequestService(AppDbContext db, ICurrentUser currentUser) : ILeaveRequestService
{
    private static readonly Expression<Func<LeaveRequest, LeaveRequestDto>> ToDto = l => new LeaveRequestDto(
        l.Id, l.UserId, l.User.Username, l.User.FullName, l.User.DepartmentId,
        l.User.Department != null ? l.User.Department.Name : null,
        l.FromDate, l.ToDate, l.ToDate.DayNumber - l.FromDate.DayNumber + 1, l.Reason, l.Status.ToString(),
        l.ReviewerId, l.Reviewer != null ? l.Reviewer.FullName : null, l.ReviewNote, l.ReviewedAt, l.CreatedAt);

    public async Task<LeaveRequestDto> CreateAsync(SaveLeaveRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        var (from, to) = ValidateRange(request);
        await EnsureNoOverlapAsync(userId, from, to, null, ct);

        var leave = new LeaveRequest
        {
            UserId = userId,
            FromDate = from,
            ToDate = to,
            Reason = request.Reason.Trim(),
            Status = LeaveStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.LeaveRequests.Add(leave);
        await db.SaveChangesAsync(ct);
        return await GetDtoAsync(leave.Id, ct);
    }

    public async Task<LeaveRequestDto> UpdateAsync(int id, SaveLeaveRequest request, CancellationToken ct = default)
    {
        var leave = await LoadOwnPendingAsync(id, "sửa", ct);
        var (from, to) = ValidateRange(request);
        await EnsureNoOverlapAsync(leave.UserId, from, to, id, ct);

        leave.FromDate = from;
        leave.ToDate = to;
        leave.Reason = request.Reason.Trim();
        await db.SaveChangesAsync(ct);
        return await GetDtoAsync(id, ct);
    }

    public async Task<LeaveRequestDto> CancelAsync(int id, CancellationToken ct = default)
    {
        var leave = await LoadOwnPendingAsync(id, "hủy", ct);
        leave.Status = LeaveStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return await GetDtoAsync(id, ct);
    }

    public async Task<PagedResult<LeaveRequestDto>> ListMineAsync(MyLeaveQuery query, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        var q = FilterStatus(db.LeaveRequests.AsNoTracking().Where(l => l.UserId == userId), query.Status);
        return await q.OrderByDescending(l => l.CreatedAt).Select(ToDto).ToPagedAsync(query, ct);
    }

    public async Task<PagedResult<LeaveRequestDto>> ListAsync(LeaveQuery query, CancellationToken ct = default)
    {
        var scope = await ApproveScopeAsync(ct);
        var q = db.LeaveRequests.AsNoTracking().AsQueryable();
        if (scope is not null)
            q = q.Where(l => l.User.DepartmentId != null && scope.Contains(l.User.DepartmentId.Value));

        q = FilterStatus(q, query.Status);
        if (query.DepartmentId is { } departmentId) q = q.Where(l => l.User.DepartmentId == departmentId);
        if (query.From > query.To) throw new AppException(400, "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc");
        // Lọc theo khoảng: lấy đơn có giao với [from, to]
        if (query.From is { } from) q = q.Where(l => l.ToDate >= from);
        if (query.To is { } to) q = q.Where(l => l.FromDate <= to);

        return await q.OrderBy(l => l.Status == LeaveStatus.Pending ? 0 : 1).ThenByDescending(l => l.CreatedAt)
            .Select(ToDto).ToPagedAsync(query, ct);
    }

    public async Task<LeaveRequestDto> GetAsync(int id, CancellationToken ct = default)
    {
        var dto = await GetDtoAsync(id, ct);
        if (dto.UserId == currentUser.UserId) return dto;

        var scope = await ApproveScopeAsync(ct);
        EnsureInScope(scope, dto.DepartmentId);
        return dto;
    }

    public Task<LeaveRequestDto> ApproveAsync(int id, string? note, CancellationToken ct = default) =>
        ReviewAsync(id, LeaveStatus.Approved, note, ct);

    public Task<LeaveRequestDto> RejectAsync(int id, string note, CancellationToken ct = default) =>
        ReviewAsync(id, LeaveStatus.Rejected, note, ct);

    private async Task<LeaveRequestDto> ReviewAsync(int id, LeaveStatus decision, string? note, CancellationToken ct)
    {
        var scope = await ApproveScopeAsync(ct);
        var leave = await db.LeaveRequests.Include(l => l.User).FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw NotFound();

        var me = await currentUser.GetAsync(ct);
        if (leave.UserId == me.Id)
            throw new AppException(403, "Không thể tự duyệt đơn nghỉ phép của chính mình");
        EnsureInScope(scope, leave.User.DepartmentId);
        if (leave.Status != LeaveStatus.Pending)
            throw new AppException(400, "Đơn đã được xử lý trước đó");

        leave.Status = decision;
        leave.ReviewerId = me.Id;
        leave.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        leave.ReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetDtoAsync(id, ct);
    }

    private Task<IReadOnlyList<int>?> ApproveScopeAsync(CancellationToken ct) =>
        currentUser.GetDepartmentScopeAsync(PermissionCodes.LeaveApproveAll, PermissionCodes.LeaveApproveTeam, ct);

    private async Task<LeaveRequest> LoadOwnPendingAsync(int id, string action, CancellationToken ct)
    {
        var leave = await db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw NotFound();
        if (leave.UserId != currentUser.UserId)
            throw new AppException(403, CurrentUserExtensions.ForbiddenMessage);
        if (leave.Status != LeaveStatus.Pending)
            throw new AppException(400, $"Chỉ {action} được đơn đang chờ duyệt");
        return leave;
    }

    private static (DateOnly From, DateOnly To) ValidateRange(SaveLeaveRequest request)
    {
        var from = request.FromDate!.Value;
        var to = request.ToDate!.Value;
        if (from > to)
            throw new AppException(400, "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc");
        return (from, to);
    }

    private async Task EnsureNoOverlapAsync(int userId, DateOnly from, DateOnly to, int? exceptId, CancellationToken ct)
    {
        var overlap = await db.LeaveRequests.AnyAsync(l =>
            l.UserId == userId && l.Id != exceptId
            && (l.Status == LeaveStatus.Pending || l.Status == LeaveStatus.Approved)
            && l.FromDate <= to && l.ToDate >= from, ct);
        if (overlap)
            throw new AppException(409, "Khoảng ngày nghỉ bị trùng với đơn khác đang chờ duyệt hoặc đã duyệt");
    }

    private static IQueryable<LeaveRequest> FilterStatus(IQueryable<LeaveRequest> q, string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return q;
        if (!Enum.TryParse<LeaveStatus>(status, true, out var parsed))
            throw new AppException(400, "Trạng thái chỉ nhận Pending, Approved, Rejected, Cancelled");
        return q.Where(l => l.Status == parsed);
    }

    private static void EnsureInScope(IReadOnlyList<int>? scope, int? departmentId)
    {
        if (scope is null) return;
        if (departmentId is null || !scope.Contains(departmentId.Value))
            throw new AppException(403, CurrentUserExtensions.ForbiddenMessage);
    }

    private async Task<LeaveRequestDto> GetDtoAsync(int id, CancellationToken ct) =>
        await db.LeaveRequests.AsNoTracking().Where(l => l.Id == id).Select(ToDto).FirstOrDefaultAsync(ct)
        ?? throw NotFound();

    private static AppException NotFound() => new(404, "Không tìm thấy đơn nghỉ phép");
}
