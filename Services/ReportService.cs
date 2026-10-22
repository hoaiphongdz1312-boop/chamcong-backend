using System.Text;
using Microsoft.EntityFrameworkCore;
using NguyenHoaiPhong_1681.Authorization;
using NguyenHoaiPhong_1681.Data;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Entities;
using NguyenHoaiPhong_1681.Enums;
using NguyenHoaiPhong_1681.Helpers;
using NguyenHoaiPhong_1681.Middleware;

namespace NguyenHoaiPhong_1681.Services;

public interface IReportService
{
    Task<MonthlyReportDto> MonthlyAsync(MonthlyReportQuery query, CancellationToken ct = default);
    Task<byte[]> MonthlyCsvAsync(MonthlyReportQuery query, CancellationToken ct = default);
}

public class ReportService(AppDbContext db, ICurrentUser currentUser) : IReportService
{
    public async Task<MonthlyReportDto> MonthlyAsync(MonthlyReportQuery query, CancellationToken ct = default)
    {
        var scope = await currentUser.GetDepartmentScopeAsync(
            PermissionCodes.AttendanceViewAll, PermissionCodes.AttendanceViewTeam, ct);
        if (query.DepartmentId is { } requested && scope is not null && !scope.Contains(requested))
            throw new AppException(403, CurrentUserExtensions.ForbiddenMessage);

        var first = DateOnly.ParseExact(query.Month + "-01", "yyyy-MM-dd");
        var last = first.AddMonths(1).AddDays(-1);

        var users = db.Users.AsNoTracking().Where(u => u.Status == UserStatus.Active && u.Role.Code != Role.Admin);
        if (scope is not null) users = users.Where(u => u.DepartmentId != null && scope.Contains(u.DepartmentId.Value));
        if (query.DepartmentId is { } departmentId) users = users.Where(u => u.DepartmentId == departmentId);

        var people = await users.OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.Username, u.FullName, u.DepartmentId, DepartmentName = u.Department != null ? u.Department.Name : null })
            .ToListAsync(ct);
        var ids = people.Select(p => p.Id).ToList();

        var records = await db.AttendanceRecords.AsNoTracking()
            .Where(r => ids.Contains(r.UserId) && r.WorkDate >= first && r.WorkDate <= last)
            .Select(r => new { r.UserId, r.WorkDate, r.Status, r.WorkMinutes })
            .ToListAsync(ct);
        var leaves = await db.LeaveRequests.AsNoTracking()
            .Where(l => ids.Contains(l.UserId) && l.Status == LeaveStatus.Approved && l.FromDate <= last && l.ToDate >= first)
            .Select(l => new { l.UserId, l.FromDate, l.ToDate })
            .ToListAsync(ct);

        // Ngày làm việc tính từ đầu tháng đến hôm nay, thứ Hai đến thứ Sáu
        var until = VietnamTime.Today < last ? VietnamTime.Today : last;
        var workingDays = new List<DateOnly>();
        for (var d = first; d <= until; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) workingDays.Add(d);

        var rows = new List<MonthlyReportRowDto>();
        foreach (var p in people)
        {
            var mine = records.Where(r => r.UserId == p.Id).ToList();
            var workDates = mine.Select(r => r.WorkDate).ToHashSet();
            var leaveDates = new HashSet<DateOnly>();
            foreach (var l in leaves.Where(l => l.UserId == p.Id))
                for (var d = l.FromDate < first ? first : l.FromDate; d <= (l.ToDate > last ? last : l.ToDate); d = d.AddDays(1))
                    leaveDates.Add(d);

            rows.Add(new MonthlyReportRowDto(
                p.Id, p.Username, p.FullName, p.DepartmentId, p.DepartmentName,
                workDates.Count,
                mine.Count(r => r.Status is AttendanceStatus.Late or AttendanceStatus.LateAndEarlyLeave),
                mine.Count(r => r.Status is AttendanceStatus.EarlyLeave or AttendanceStatus.LateAndEarlyLeave),
                mine.Sum(r => r.WorkMinutes),
                leaveDates.Count,
                workingDays.Count(d => !workDates.Contains(d) && !leaveDates.Contains(d))));
        }

        return new MonthlyReportDto(query.Month, rows.Count, rows.Sum(r => r.WorkDays), rows.Sum(r => r.LateCount),
            rows.Sum(r => r.WorkMinutes), rows.Sum(r => r.LeaveDays), rows.Sum(r => r.AbsentDays), rows);
    }

    public async Task<byte[]> MonthlyCsvAsync(MonthlyReportQuery query, CancellationToken ct = default)
    {
        var report = await MonthlyAsync(query, ct);
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", "Tên đăng nhập", "Họ tên", "Phòng ban", "Ngày công", "Đi muộn", "Về sớm", "Giờ làm", "Ngày nghỉ phép", "Vắng mặt"));
        foreach (var r in report.Rows)
            sb.AppendLine(string.Join(",", Cell(r.Username), Cell(r.FullName), Cell(r.DepartmentName ?? ""), r.WorkDays, r.LateCount,
                r.EarlyLeaveCount, Math.Round(r.WorkMinutes / 60.0, 1).ToString(System.Globalization.CultureInfo.InvariantCulture), r.LeaveDays, r.AbsentDays));
        // BOM để Excel đọc đúng tiếng Việt
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    /// <summary>Escape CSV, đồng thời chặn công thức Excel (=, +, -, @ ở đầu ô).</summary>
    private static string Cell(string value)
    {
        if (value.Length > 0 && "=+-@".Contains(value[0])) value = "'" + value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
