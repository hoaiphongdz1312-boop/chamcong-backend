using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NguyenHoaiPhong_1681.Authorization;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Services;

namespace NguyenHoaiPhong_1681.Controllers;

/// <summary>Chấm công.</summary>
[ApiController]
[Route("api/attendance")]
public class AttendanceController(IAttendanceService service) : ControllerBase
{
    /// <summary>Check-in ca làm hôm nay (giờ Việt Nam). Trễ quá số phút cho phép → Late.</summary>
    [HttpPost("check-in")]
    [HasPermission(PermissionCodes.AttendanceSelf)]
    public async Task<ActionResult<AttendanceDto>> CheckIn(CheckInOutRequest request, CancellationToken ct)
        => Ok(await service.CheckInAsync(request, ct));

    /// <summary>Check-out ca làm; về trước giờ kết thúc → EarlyLeave; tính số phút làm.</summary>
    [HttpPost("check-out")]
    [HasPermission(PermissionCodes.AttendanceSelf)]
    public async Task<ActionResult<AttendanceDto>> CheckOut(CheckInOutRequest request, CancellationToken ct)
        => Ok(await service.CheckOutAsync(request, ct));

    /// <summary>Lịch sử chấm công của tôi.</summary>
    [HttpGet("my")]
    [HasPermission(PermissionCodes.AttendanceSelf)]
    public async Task<ActionResult<PagedResult<AttendanceDto>>> My([FromQuery] MyAttendanceQuery query, CancellationToken ct)
        => Ok(await service.ListMineAsync(query, ct));

    /// <summary>Danh sách chấm công: Manager (attendance.view_team, phòng mình) / Admin (attendance.view_all).</summary>
    [HttpGet]
    [HasPermission($"{PermissionCodes.AttendanceViewTeam}|{PermissionCodes.AttendanceViewAll}")]
    public async Task<ActionResult<PagedResult<AttendanceDto>>> List([FromQuery] AttendanceQuery query, CancellationToken ct)
        => Ok(await service.ListAsync(query, ct));

    /// <summary>Chi tiết bản ghi chấm công (của mình, hoặc trong phạm vi được xem).</summary>
    [HttpGet("{id:int}")]
    [HasPermission($"{PermissionCodes.AttendanceSelf}|{PermissionCodes.AttendanceViewTeam}|{PermissionCodes.AttendanceViewAll}")]
    public async Task<ActionResult<AttendanceDto>> Get(int id, CancellationToken ct)
        => Ok(await service.GetAsync(id, ct));

    /// <summary>Sửa chấm công (bắt buộc ghi chú; Manager chỉ sửa được phòng mình).</summary>
    [HttpPut("{id:int}")]
    [HasPermission(PermissionCodes.AttendanceEdit)]
    public async Task<ActionResult<AttendanceDto>> Update(int id, UpdateAttendanceRequest request, CancellationToken ct)
        => Ok(await service.UpdateAsync(id, request, ct));

    /// <summary>Xóa bản ghi chấm công (chỉ Admin).</summary>
    [HttpDelete("{id:int}")]
    [HasPermission(PermissionCodes.AttendanceDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
