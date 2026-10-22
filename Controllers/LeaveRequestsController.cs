using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NguyenHoaiPhong_1681.Authorization;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Services;

namespace NguyenHoaiPhong_1681.Controllers;

/// <summary>Đơn nghỉ phép.</summary>
[ApiController]
[Route("api/leave-requests")]
public class LeaveRequestsController(ILeaveRequestService service) : ControllerBase
{
    /// <summary>Tạo đơn nghỉ phép (không được trùng khoảng với đơn đang chờ/đã duyệt).</summary>
    [HttpPost]
    [HasPermission(PermissionCodes.LeaveSelf)]
    public async Task<ActionResult<LeaveRequestDto>> Create(SaveLeaveRequest request, CancellationToken ct)
    {
        var leave = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = leave.Id }, leave);
    }

    /// <summary>Sửa đơn của mình khi còn Pending.</summary>
    [HttpPut("{id:int}")]
    [HasPermission(PermissionCodes.LeaveSelf)]
    public async Task<ActionResult<LeaveRequestDto>> Update(int id, SaveLeaveRequest request, CancellationToken ct)
        => Ok(await service.UpdateAsync(id, request, ct));

    /// <summary>Hủy đơn của mình khi còn Pending.</summary>
    [HttpPost("{id:int}/cancel")]
    [HasPermission(PermissionCodes.LeaveSelf)]
    public async Task<ActionResult<LeaveRequestDto>> Cancel(int id, CancellationToken ct)
        => Ok(await service.CancelAsync(id, ct));

    /// <summary>Đơn nghỉ phép của tôi.</summary>
    [HttpGet("my")]
    [HasPermission(PermissionCodes.LeaveSelf)]
    public async Task<ActionResult<PagedResult<LeaveRequestDto>>> My([FromQuery] MyLeaveQuery query, CancellationToken ct)
        => Ok(await service.ListMineAsync(query, ct));

    /// <summary>Danh sách đơn cần duyệt: Manager (leave.approve_team, phòng mình) / Admin (leave.approve_all).</summary>
    [HttpGet]
    [HasPermission($"{PermissionCodes.LeaveApproveTeam}|{PermissionCodes.LeaveApproveAll}")]
    public async Task<ActionResult<PagedResult<LeaveRequestDto>>> List([FromQuery] LeaveQuery query, CancellationToken ct)
        => Ok(await service.ListAsync(query, ct));

    /// <summary>Chi tiết đơn (của mình, hoặc trong phạm vi được duyệt).</summary>
    [HttpGet("{id:int}")]
    [HasPermission($"{PermissionCodes.LeaveSelf}|{PermissionCodes.LeaveApproveTeam}|{PermissionCodes.LeaveApproveAll}")]
    public async Task<ActionResult<LeaveRequestDto>> Get(int id, CancellationToken ct)
        => Ok(await service.GetAsync(id, ct));

    /// <summary>Duyệt đơn (không tự duyệt đơn của mình).</summary>
    [HttpPost("{id:int}/approve")]
    [HasPermission($"{PermissionCodes.LeaveApproveTeam}|{PermissionCodes.LeaveApproveAll}")]
    public async Task<ActionResult<LeaveRequestDto>> Approve(int id, ApproveLeaveRequest? request, CancellationToken ct)
        => Ok(await service.ApproveAsync(id, request?.Note, ct));

    /// <summary>Từ chối đơn (bắt buộc ghi lý do).</summary>
    [HttpPost("{id:int}/reject")]
    [HasPermission($"{PermissionCodes.LeaveApproveTeam}|{PermissionCodes.LeaveApproveAll}")]
    public async Task<ActionResult<LeaveRequestDto>> Reject(int id, RejectLeaveRequest request, CancellationToken ct)
        => Ok(await service.RejectAsync(id, request.Note, ct));
}
