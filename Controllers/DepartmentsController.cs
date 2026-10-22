using Microsoft.AspNetCore.Mvc;
using NguyenHoaiPhong_1681.Authorization;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Services;

namespace NguyenHoaiPhong_1681.Controllers;

/// <summary>Phòng ban.</summary>
[ApiController]
[Route("api/departments")]
public class DepartmentsController(IDepartmentService service) : ControllerBase
{
    /// <summary>Danh sách phòng ban (Manager chỉ thấy phòng mình).</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.DepartmentsView)]
    public async Task<ActionResult<PagedResult<DepartmentDto>>> List([FromQuery] DepartmentListQuery query, CancellationToken ct)
        => Ok(await service.ListAsync(query, ct));

    /// <summary>Chi tiết phòng ban.</summary>
    [HttpGet("{id:int}")]
    [HasPermission(PermissionCodes.DepartmentsView)]
    public async Task<ActionResult<DepartmentDto>> Get(int id, CancellationToken ct)
        => Ok(await service.GetAsync(id, ct));

    /// <summary>Tạo phòng ban.</summary>
    [HttpPost]
    [HasPermission(PermissionCodes.DepartmentsManage)]
    public async Task<ActionResult<DepartmentDto>> Create(SaveDepartmentRequest request, CancellationToken ct)
    {
        var department = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = department.Id }, department);
    }

    /// <summary>Sửa phòng ban.</summary>
    [HttpPut("{id:int}")]
    [HasPermission(PermissionCodes.DepartmentsManage)]
    public async Task<ActionResult<DepartmentDto>> Update(int id, SaveDepartmentRequest request, CancellationToken ct)
        => Ok(await service.UpdateAsync(id, request, ct));

    /// <summary>Xóa mềm phòng ban (còn nhân viên → 409).</summary>
    [HttpDelete("{id:int}")]
    [HasPermission(PermissionCodes.DepartmentsManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Đặt người quản lý (phải có role Manager).</summary>
    [HttpPatch("{id:int}/manager")]
    [HasPermission(PermissionCodes.DepartmentsManage)]
    public async Task<ActionResult<DepartmentDto>> SetManager(int id, SetDepartmentManagerRequest request, CancellationToken ct)
        => Ok(await service.SetManagerAsync(id, request.UserId!.Value, ct));

    /// <summary>Nhân viên của phòng ban (Admin mọi phòng; Manager chỉ phòng mình quản lý, sai phòng → 403).</summary>
    [HttpGet("{id:int}/members")]
    [HasPermission(PermissionCodes.DepartmentsView)]
    public async Task<ActionResult<IReadOnlyList<DepartmentMemberDto>>> Members(int id, CancellationToken ct)
        => Ok(await service.GetMembersAsync(id, ct));
}
