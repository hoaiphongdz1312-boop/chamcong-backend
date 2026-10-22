using Microsoft.AspNetCore.Mvc;
using NguyenHoaiPhong_1681.Authorization;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Services;

namespace NguyenHoaiPhong_1681.Controllers;

/// <summary>Báo cáo: Manager (attendance.view_team, phòng mình) / Admin (attendance.view_all).</summary>
[ApiController]
[Route("api/reports")]
[HasPermission($"{PermissionCodes.AttendanceViewTeam}|{PermissionCodes.AttendanceViewAll}")]
public class ReportsController(IReportService service) : ControllerBase
{
    /// <summary>Bảng công tháng: ngày công, đi muộn, về sớm, giờ làm, nghỉ phép, vắng mặt của từng nhân viên.</summary>
    [HttpGet("attendance-monthly")]
    public async Task<ActionResult<MonthlyReportDto>> Monthly([FromQuery] MonthlyReportQuery query, CancellationToken ct)
        => Ok(await service.MonthlyAsync(query, ct));

    /// <summary>Xuất bảng công tháng ra file CSV (mở được bằng Excel).</summary>
    [HttpGet("attendance-monthly/export")]
    public async Task<IActionResult> MonthlyExport([FromQuery] MonthlyReportQuery query, CancellationToken ct)
        => File(await service.MonthlyCsvAsync(query, ct), "text/csv; charset=utf-8", $"bang-cong-{query.Month}.csv");
}
