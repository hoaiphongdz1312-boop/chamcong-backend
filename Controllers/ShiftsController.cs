using Microsoft.AspNetCore.Mvc;
using NguyenHoaiPhong_1681.Authorization;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Services;

namespace NguyenHoaiPhong_1681.Controllers;

/// <summary>Ca làm.</summary>
[ApiController]
[Route("api/shifts")]
public class ShiftsController(IShiftService service) : ControllerBase
{
    /// <summary>Danh sách ca làm (?activeOnly=true chỉ lấy ca đang hoạt động).</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.ShiftsView)]
    public async Task<ActionResult<PagedResult<ShiftDto>>> List([FromQuery] ShiftListQuery query, CancellationToken ct)
        => Ok(await service.ListAsync(query, ct));

    /// <summary>Chi tiết ca làm.</summary>
    [HttpGet("{id:int}")]
    [HasPermission(PermissionCodes.ShiftsView)]
    public async Task<ActionResult<ShiftDto>> Get(int id, CancellationToken ct)
        => Ok(await service.GetAsync(id, ct));

    /// <summary>Tạo ca làm (giờ dạng "HH:mm").</summary>
    [HttpPost]
    [HasPermission(PermissionCodes.ShiftsManage)]
    public async Task<ActionResult<ShiftDto>> Create(SaveShiftRequest request, CancellationToken ct)
    {
        var shift = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = shift.Id }, shift);
    }

    /// <summary>Sửa ca làm.</summary>
    [HttpPut("{id:int}")]
    [HasPermission(PermissionCodes.ShiftsManage)]
    public async Task<ActionResult<ShiftDto>> Update(int id, SaveShiftRequest request, CancellationToken ct)
        => Ok(await service.UpdateAsync(id, request, ct));

    /// <summary>Xóa mềm ca làm.</summary>
    [HttpDelete("{id:int}")]
    [HasPermission(PermissionCodes.ShiftsManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
