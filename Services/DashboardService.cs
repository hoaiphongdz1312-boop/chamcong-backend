using Microsoft.EntityFrameworkCore;
using NguyenHoaiPhong_1681.Data;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Enums;
using NguyenHoaiPhong_1681.Helpers;
using NguyenHoaiPhong_1681.Middleware;

namespace NguyenHoaiPhong_1681.Services;

public interface IDashboardService
{
    Task<AdminDashboardDto> AdminAsync(CancellationToken ct = default);
    Task<ManagerDashboardDto> ManagerAsync(CancellationToken ct = default);
    Task<EmployeeDashboardDto> EmployeeAsync(CancellationToken ct = default);
}

public class DashboardService(AppDbContext db, ICurrentUser currentUser, IDepartmentService departmentService) : IDashboardService
{
    private static readonly AttendanceStatus[] LateStatuses = [AttendanceStatus.Late, AttendanceStatus.LateAndEarlyLeave];

    public async Task<AdminDashboardDto> AdminAsync(CancellationToken ct = default)
    {
        var today = VietnamTime.Today;
        var users = db.Users.AsNoTracking();

        var byRole = await users
            .GroupBy(u => new { u.Role.Code, u.Role.Name })
            .Select(g => new CountItem(g.Key.Code, g.Key.Name, g.Count()))
            .ToListAsync(ct);
        var byDepartment = await users
            .Where(u => u.DepartmentId != null)
            .GroupBy(u => new { u.DepartmentId, u.Department!.Name })
            .Select(g => new CountItem(g.Key.DepartmentId!.Value.ToString(), g.Key.Name, g.Count()))
            .ToListAsync(ct);

        var todayRecords = db.AttendanceRecords.AsNoTracking().Where(r => r.WorkDate == today);
        return new AdminDashboardDto(
            await users.CountAsync(ct),
            await users.CountAsync(u => u.Status == UserStatus.PendingApproval, ct),
            await users.CountAsync(u => u.Status == UserStatus.Locked, ct),
            byRole,
            byDepartment,
            await todayRecords.CountAsync(ct),
            await todayRecords.CountAsync(r => LateStatuses.Contains(r.Status), ct),
            await db.LeaveRequests.CountAsync(l => l.Status == LeaveStatus.Pending, ct));
    }

    public async Task<ManagerDashboardDto> ManagerAsync(CancellationToken ct = default)
    {
        var today = VietnamTime.Today;
        var managed = await currentUser.GetManagedDepartmentIdsAsync(ct);
        var departments = new List<DepartmentDto>();
        foreach (var id in managed) departments.Add(await departmentService.GetAsync(id, ct));

        var teamIds = await db.Users.AsNoTracking()
            .Where(u => u.DepartmentId != null && managed.Contains(u.DepartmentId.Value) && u.Status == UserStatus.Active)
            .Select(u => u.Id)
            .ToListAsync(ct);

        var todayRecords = db.AttendanceRecords.AsNoTracking().Where(r => r.WorkDate == today && teamIds.Contains(r.UserId));
        var present = await todayRecords.Select(r => r.UserId).Distinct().CountAsync(ct);
        var late = await todayRecords.Where(r => LateStatuses.Contains(r.Status)).Select(r => r.UserId).Distinct().CountAsync(ct);
        var onLeave = await db.LeaveRequests.AsNoTracking()
            .Where(l => teamIds.Contains(l.UserId) && l.Status == LeaveStatus.Approved && l.FromDate <= today && l.ToDate >= today)
            .Select(l => l.UserId).Distinct().CountAsync(ct);
        var pending = await db.LeaveRequests.CountAsync(l => teamIds.Contains(l.UserId) && l.Status == LeaveStatus.Pending, ct);

        return new ManagerDashboardDto(departments, teamIds.Count, present, late, onLeave,
            Math.Max(0, teamIds.Count - present - onLeave), pending);
    }

    public async Task<EmployeeDashboardDto> EmployeeAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        var today = VietnamTime.Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var shifts = await db.Shifts.AsNoTracking().Where(s => s.IsActive && !s.IsDeleted).OrderBy(s => s.StartTime).ToListAsync(ct);
        var todayRecords = await db.AttendanceRecords.AsNoTracking()
            .Where(r => r.UserId == userId && r.WorkDate == today)
            .ToDictionaryAsync(r => r.ShiftId, ct);
        var shiftsToday = shifts.Select(s =>
        {
            todayRecords.TryGetValue(s.Id, out var r);
            return new EmployeeShiftToday(s.Id, s.Name, ShiftService.FormatTime(s.StartTime), ShiftService.FormatTime(s.EndTime),
                r?.Id, r?.CheckInAt, r?.CheckOutAt, r?.Status.ToString());
        }).ToList();

        var monthRecords = db.AttendanceRecords.AsNoTracking()
            .Where(r => r.UserId == userId && r.WorkDate >= monthStart && r.WorkDate <= monthEnd);
        var workDays = await monthRecords.Where(r => r.CheckOutAt != null).Select(r => r.WorkDate).Distinct().CountAsync(ct);
        var lateCount = await monthRecords.CountAsync(r => LateStatuses.Contains(r.Status), ct);

        // Số ngày nghỉ được duyệt rơi vào tháng này (cắt theo biên tháng)
        var approved = await db.LeaveRequests.AsNoTracking()
            .Where(l => l.UserId == userId && l.Status == LeaveStatus.Approved && l.FromDate <= monthEnd && l.ToDate >= monthStart)
            .Select(l => new { l.FromDate, l.ToDate })
            .ToListAsync(ct);
        var leaveDays = approved.Sum(l =>
            (l.ToDate > monthEnd ? monthEnd : l.ToDate).DayNumber - (l.FromDate < monthStart ? monthStart : l.FromDate).DayNumber + 1);
        var pending = await db.LeaveRequests.CountAsync(l => l.UserId == userId && l.Status == LeaveStatus.Pending, ct);

        return new EmployeeDashboardDto(today, shiftsToday, workDays, lateCount, leaveDays, pending);
    }
}
