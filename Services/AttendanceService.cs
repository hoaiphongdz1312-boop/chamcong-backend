using System.Linq.Expressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NguyenHoaiPhong_1681.Authorization;
using NguyenHoaiPhong_1681.Data;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Entities;
using NguyenHoaiPhong_1681.Enums;
using NguyenHoaiPhong_1681.Helpers;
using NguyenHoaiPhong_1681.Middleware;

namespace NguyenHoaiPhong_1681.Services;

public interface IAttendanceService
{
    Task<AttendanceDto> CheckInAsync(CheckInOutRequest request, CancellationToken ct = default);
    Task<AttendanceDto> CheckOutAsync(CheckInOutRequest request, CancellationToken ct = default);
    Task AutoCheckInAsync(int userId, CancellationToken ct = default);
    Task<PagedResult<AttendanceDto>> ListMineAsync(MyAttendanceQuery query, CancellationToken ct = default);
    Task<PagedResult<AttendanceDto>> ListAsync(AttendanceQuery query, CancellationToken ct = default);
    Task<AttendanceDto> GetAsync(int id, CancellationToken ct = default);
    Task<AttendanceDto> UpdateAsync(int id, UpdateAttendanceRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class AttendanceService(AppDbContext db, ICurrentUser currentUser, IAttendanceProofService proof) : IAttendanceService
{
    private static readonly Expression<Func<AttendanceRecord, AttendanceDto>> ToDto = r => new AttendanceDto(
        r.Id, r.UserId, r.User.Username, r.User.FullName, r.User.DepartmentId,
        r.User.Department != null ? r.User.Department.Name : null,
        r.ShiftId, r.Shift.Name, r.WorkDate, r.CheckInAt, r.CheckOutAt, r.Status.ToString(),
        r.WorkMinutes, r.Note, r.EditedById, r.EditedAt, r.CreatedAt,
        r.CheckInLatitude, r.CheckInLongitude, r.CheckInDistanceMeters,
        r.CheckOutLatitude, r.CheckOutLongitude, r.CheckOutDistanceMeters);

    public async Task<AttendanceDto> CheckInAsync(CheckInOutRequest request, CancellationToken ct = default)
    {
        var distance = proof.Verify(request);
        var userId = currentUser.UserId;
        var shiftId = request.ShiftId ?? await ResolveShiftIdAsync(ct);
        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId && !s.IsDeleted, ct)
                    ?? throw new AppException(404, "Không tìm thấy ca làm");
        if (!shift.IsActive)
            throw new AppException(400, "Ca làm đang tạm ngưng");

        var nowUtc = DateTime.UtcNow;
        var today = VietnamTime.Today;
        if (await db.AttendanceRecords.AnyAsync(r => r.UserId == userId && r.ShiftId == shiftId && r.WorkDate == today, ct))
            throw new AppException(409, "Bạn đã check-in ca này hôm nay");

        var lateAfter = VietnamTime.ToUtc(today, shift.StartTime).AddMinutes(shift.LateToleranceMinutes);
        var record = new AttendanceRecord
        {
            UserId = userId,
            ShiftId = shiftId,
            WorkDate = today,
            CheckInAt = nowUtc,
            Status = nowUtc > lateAfter ? AttendanceStatus.Late : AttendanceStatus.OnTime,
            CheckInLatitude = request.Latitude,
            CheckInLongitude = request.Longitude,
            CheckInDistanceMeters = distance,
            CreatedAt = nowUtc
        };
        db.AttendanceRecords.Add(record);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new AppException(409, "Bạn đã check-in ca này hôm nay");
        }

        return await GetDtoAsync(record.Id, ct);
    }

    /// <summary>Không truyền shiftId: chọn ca đang diễn ra, nếu không có thì ca có giờ bắt đầu gần giờ hiện tại nhất.</summary>
    private async Task<int> ResolveShiftIdAsync(CancellationToken ct)
    {
        var shifts = await db.Shifts.AsNoTracking().Where(s => s.IsActive && !s.IsDeleted).ToListAsync(ct);
        if (shifts.Count == 0) throw new AppException(400, "Chưa có ca làm nào đang hoạt động");

        var nowUtc = DateTime.UtcNow;
        var today = VietnamTime.Today;
        var userId = currentUser.UserId;
        foreach (var date in new[] { today, today.AddDays(-1) })
        {
            var ongoing = shifts.Where(s => VietnamTime.ToUtc(date, s.StartTime) <= nowUtc && nowUtc < ShiftEndUtc(date, s))
                .OrderBy(s => s.StartTime).ToList();
            foreach (var s in ongoing)
                if (!await db.AttendanceRecords.AnyAsync(r => r.UserId == userId && r.ShiftId == s.Id && r.WorkDate == date, ct))
                    return s.Id;
        }

        var nearest = shifts.OrderBy(s => Math.Abs((nowUtc - VietnamTime.ToUtc(today, s.StartTime)).TotalMinutes)).First();
        return nearest.Id;
    }


    /// <summary>Tự check-in khi nhân viên đăng nhập: ca đang diễn ra (kể cả ca qua đêm bắt đầu hôm qua) mà chưa có bản ghi. Không có ca thì bỏ qua.</summary>
    public async Task AutoCheckInAsync(int userId, CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var today = VietnamTime.Today;
        var shifts = await db.Shifts.AsNoTracking().Where(s => s.IsActive && !s.IsDeleted).OrderBy(s => s.StartTime).ToListAsync(ct);
        foreach (var date in new[] { today, today.AddDays(-1) })
        {
            foreach (var shift in shifts)
            {
                var startUtc = VietnamTime.ToUtc(date, shift.StartTime);
                if (nowUtc < startUtc || nowUtc >= ShiftEndUtc(date, shift)) continue;
                if (await db.AttendanceRecords.AnyAsync(r => r.UserId == userId && r.ShiftId == shift.Id && r.WorkDate == date, ct)) return;

                db.AttendanceRecords.Add(new AttendanceRecord
                {
                    UserId = userId,
                    ShiftId = shift.Id,
                    WorkDate = date,
                    CheckInAt = nowUtc,
                    Status = nowUtc > startUtc.AddMinutes(shift.LateToleranceMinutes) ? AttendanceStatus.Late : AttendanceStatus.OnTime,
                    Note = "Tự động check-in khi đăng nhập",
                    CreatedAt = nowUtc
                });
                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
                {
                    // Đã có bản ghi do request khác tạo
                }
                return;
            }
        }
    }

    public async Task<AttendanceDto> CheckOutAsync(CheckInOutRequest request, CancellationToken ct = default)
    {
        var distance = proof.Verify(request);
        var userId = currentUser.UserId;
        var today = VietnamTime.Today;
        var yesterday = today.AddDays(-1);
        var shiftId = request.ShiftId ?? await db.AttendanceRecords
            .Where(r => r.UserId == userId && r.CheckOutAt == null && (r.WorkDate == today || r.WorkDate == yesterday))
            .OrderByDescending(r => r.CheckInAt).Select(r => (int?)r.ShiftId).FirstOrDefaultAsync(ct)
            ?? throw new AppException(400, "Bạn chưa check-in");

        // Ca qua đêm: bản ghi check-in có thể thuộc ngày hôm trước
        var records = await db.AttendanceRecords
            .Include(r => r.Shift)
            .Where(r => r.UserId == userId && r.ShiftId == shiftId && (r.WorkDate == today || r.WorkDate == yesterday))
            .OrderByDescending(r => r.WorkDate)
            .ToListAsync(ct);

        var record = records.FirstOrDefault(r => r.CheckOutAt is null);
        if (record is null)
        {
            if (records.Any(r => r.WorkDate == today))
                throw new AppException(409, "Bạn đã check-out ca này hôm nay");
            throw new AppException(400, "Bạn chưa check-in ca này");
        }

        var nowUtc = DateTime.UtcNow;
        record.CheckOutAt = nowUtc;
        record.WorkMinutes = WorkMinutes(record.CheckInAt, nowUtc);
        record.CheckOutLatitude = request.Latitude;
        record.CheckOutLongitude = request.Longitude;
        record.CheckOutDistanceMeters = distance;

        var shiftEnd = ShiftEndUtc(record.WorkDate, record.Shift);
        if (nowUtc < shiftEnd)
        {
            record.Status = record.Status == AttendanceStatus.Late
                ? AttendanceStatus.LateAndEarlyLeave
                : AttendanceStatus.EarlyLeave;
        }

        await db.SaveChangesAsync(ct);
        return await GetDtoAsync(record.Id, ct);
    }

    public async Task<PagedResult<AttendanceDto>> ListMineAsync(MyAttendanceQuery query, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        var q = db.AttendanceRecords.AsNoTracking().Where(r => r.UserId == userId);
        q = FilterDates(q, query.From, query.To);
        return await q.OrderByDescending(r => r.WorkDate).ThenByDescending(r => r.CheckInAt)
            .Select(ToDto).ToPagedAsync(query, ct);
    }

    public async Task<PagedResult<AttendanceDto>> ListAsync(AttendanceQuery query, CancellationToken ct = default)
    {
        var scope = await currentUser.GetDepartmentScopeAsync(
            PermissionCodes.AttendanceViewAll, PermissionCodes.AttendanceViewTeam, ct);

        var q = db.AttendanceRecords.AsNoTracking().AsQueryable();
        if (scope is not null)
            q = q.Where(r => r.User.DepartmentId != null && scope.Contains(r.User.DepartmentId.Value));

        if (query.UserId is { } userId) q = q.Where(r => r.UserId == userId);
        if (query.DepartmentId is { } departmentId) q = q.Where(r => r.User.DepartmentId == departmentId);
        if (query.ShiftId is { } shiftId) q = q.Where(r => r.ShiftId == shiftId);
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<AttendanceStatus>(query.Status, true, out var status))
                throw new AppException(400, "Trạng thái chỉ nhận OnTime, Late, EarlyLeave, LateAndEarlyLeave");
            q = q.Where(r => r.Status == status);
        }
        q = FilterDates(q, query.From, query.To);

        return await q.OrderByDescending(r => r.WorkDate).ThenByDescending(r => r.CheckInAt)
            .Select(ToDto).ToPagedAsync(query, ct);
    }

    public async Task<AttendanceDto> GetAsync(int id, CancellationToken ct = default)
    {
        var dto = await GetDtoAsync(id, ct);
        if (dto.UserId == currentUser.UserId && await currentUser.HasPermissionAsync(PermissionCodes.AttendanceSelf, ct))
            return dto;

        var scope = await currentUser.GetDepartmentScopeAsync(
            PermissionCodes.AttendanceViewAll, PermissionCodes.AttendanceViewTeam, ct);
        EnsureInScope(scope, dto.DepartmentId);
        return dto;
    }

    public async Task<AttendanceDto> UpdateAsync(int id, UpdateAttendanceRequest request, CancellationToken ct = default)
    {
        var record = await db.AttendanceRecords.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == id, ct)
                     ?? throw NotFound();

        var me = await currentUser.GetAsync(ct);
        if (!await currentUser.HasPermissionAsync(PermissionCodes.AttendanceViewAll, ct))
        {
            if (record.UserId == me.Id)
                throw new AppException(403, "Không thể tự sửa chấm công của chính mình");
            var managed = await currentUser.GetManagedDepartmentIdsAsync(ct);
            EnsureInScope(managed, record.User.DepartmentId);
        }

        var checkIn = VietnamTime.NormalizeUtc(request.CheckInAt!.Value);
        DateTime? checkOut = request.CheckOutAt is { } co ? VietnamTime.NormalizeUtc(co) : null;
        if (checkOut <= checkIn)
            throw new AppException(400, "Giờ check-out phải sau giờ check-in");

        record.CheckInAt = checkIn;
        record.CheckOutAt = checkOut;
        record.WorkMinutes = checkOut is { } end ? WorkMinutes(checkIn, end) : 0;
        record.Status = Enum.Parse<AttendanceStatus>(request.Status);
        record.Note = request.Note.Trim();
        record.EditedById = me.Id;
        record.EditedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetDtoAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var record = await db.AttendanceRecords.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw NotFound();
        db.AttendanceRecords.Remove(record);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Giờ kết thúc ca (UTC); EndTime ≤ StartTime nghĩa là kết thúc vào ngày hôm sau.</summary>
    public static DateTime ShiftEndUtc(DateOnly workDate, Shift shift) =>
        VietnamTime.ToUtc(shift.EndTime <= shift.StartTime ? workDate.AddDays(1) : workDate, shift.EndTime);

    private static int WorkMinutes(DateTime checkIn, DateTime checkOut) =>
        Math.Max(0, (int)Math.Floor((checkOut - checkIn).TotalMinutes));

    private static IQueryable<AttendanceRecord> FilterDates(IQueryable<AttendanceRecord> q, DateOnly? from, DateOnly? to)
    {
        if (from > to) throw new AppException(400, "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc");
        if (from is { } f) q = q.Where(r => r.WorkDate >= f);
        if (to is { } t) q = q.Where(r => r.WorkDate <= t);
        return q;
    }

    private static void EnsureInScope(IReadOnlyList<int>? scope, int? departmentId)
    {
        if (scope is null) return;
        if (departmentId is null || !scope.Contains(departmentId.Value))
            throw new AppException(403, CurrentUserExtensions.ForbiddenMessage);
    }

    private async Task<AttendanceDto> GetDtoAsync(int id, CancellationToken ct) =>
        await db.AttendanceRecords.AsNoTracking().Where(r => r.Id == id).Select(ToDto).FirstOrDefaultAsync(ct)
        ?? throw NotFound();

    private static AppException NotFound() => new(404, "Không tìm thấy bản ghi chấm công");
}
