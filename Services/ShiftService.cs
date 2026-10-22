using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NguyenHoaiPhong_1681.Data;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Entities;
using NguyenHoaiPhong_1681.Middleware;

namespace NguyenHoaiPhong_1681.Services;

public interface IShiftService
{
    Task<PagedResult<ShiftDto>> ListAsync(ShiftListQuery query, CancellationToken ct = default);
    Task<ShiftDto> GetAsync(int id, CancellationToken ct = default);
    Task<ShiftDto> CreateAsync(SaveShiftRequest request, CancellationToken ct = default);
    Task<ShiftDto> UpdateAsync(int id, SaveShiftRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class ShiftService(AppDbContext db) : IShiftService
{
    public async Task<PagedResult<ShiftDto>> ListAsync(ShiftListQuery query, CancellationToken ct = default)
    {
        var q = db.Shifts.AsNoTracking().Where(s => !s.IsDeleted);
        if (query.ActiveOnly == true) q = q.Where(s => s.IsActive);

        var page = await q.OrderBy(s => s.StartTime).ThenBy(s => s.Name).ToPagedAsync(query, ct);
        return new PagedResult<ShiftDto>(page.Items.Select(ToDto).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<ShiftDto> GetAsync(int id, CancellationToken ct = default) => ToDto(await LoadAsync(id, ct));

    public async Task<ShiftDto> CreateAsync(SaveShiftRequest request, CancellationToken ct = default)
    {
        var shift = new Shift { CreatedAt = DateTime.UtcNow };
        Apply(shift, request);
        db.Shifts.Add(shift);
        await db.SaveChangesAsync(ct);
        return ToDto(shift);
    }

    public async Task<ShiftDto> UpdateAsync(int id, SaveShiftRequest request, CancellationToken ct = default)
    {
        var shift = await LoadAsync(id, ct);
        Apply(shift, request);
        await db.SaveChangesAsync(ct);
        return ToDto(shift);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var shift = await LoadAsync(id, ct);
        shift.IsDeleted = true;
        shift.IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    public static string FormatTime(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static ShiftDto ToDto(Shift s) => new(
        s.Id, s.Name, FormatTime(s.StartTime), FormatTime(s.EndTime), s.LateToleranceMinutes, s.IsActive, s.CreatedAt);

    private static void Apply(Shift shift, SaveShiftRequest request)
    {
        var start = TimeOnly.ParseExact(request.StartTime, "HH:mm", CultureInfo.InvariantCulture);
        var end = TimeOnly.ParseExact(request.EndTime, "HH:mm", CultureInfo.InvariantCulture);
        if (start == end)
            throw new AppException(400, "Giờ bắt đầu và giờ kết thúc không được trùng nhau");

        shift.Name = request.Name.Trim();
        shift.StartTime = start;
        shift.EndTime = end;
        shift.LateToleranceMinutes = request.LateToleranceMinutes;
        shift.IsActive = request.IsActive;
    }

    private async Task<Shift> LoadAsync(int id, CancellationToken ct) =>
        await db.Shifts.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct)
        ?? throw new AppException(404, "Không tìm thấy ca làm");
}
