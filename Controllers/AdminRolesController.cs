using Microsoft.AspNetCore.Mvc;
using NguyenHoaiPhong_1681.Authorization;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Services;

namespace NguyenHoaiPhong_1681.Controllers;

/// <summary>Quản lý vai trò và phân quyền động.</summary>
[ApiController]
[Route("api/admin")]
[HasPermission(PermissionCodes.RolesManage)]
public class AdminRolesController(IRoleAdminService service) : ControllerBase
{
    /// <summary>Danh sách mã quyền của hệ thống.</summary>
    [HttpGet("permissions")]
    public async Task<ActionResult<IReadOnlyList<PermissionDto>>> Permissions(CancellationToken ct)
        => Ok(await service.ListPermissionsAsync(ct));

    /// <summary>Danh sách vai trò kèm quyền và số người dùng.</summary>
    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> Roles(CancellationToken ct)
        => Ok(await service.ListRolesAsync(ct));

    /// <summary>Tạo vai trò mới.</summary>
    [HttpPost("roles")]
    public async Task<ActionResult<RoleDto>> Create(CreateRoleRequest request, CancellationToken ct)
        => Ok(await service.CreateAsync(request, ct));

    /// <summary>Sửa tên, mô tả vai trò.</summary>
    [HttpPut("roles/{id:int}")]
    public async Task<ActionResult<RoleDto>> Update(int id, UpdateRoleRequest request, CancellationToken ct)
        => Ok(await service.UpdateAsync(id, request, ct));

    /// <summary>Xóa vai trò (không xóa vai trò hệ thống hoặc còn người dùng).</summary>
    [HttpDelete("roles/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Gán lại toàn bộ quyền cho vai trò (có hiệu lực ngay).</summary>
    [HttpPut("roles/{id:int}/permissions")]
    public async Task<ActionResult<RoleDto>> SetPermissions(int id, SetRolePermissionsRequest request, CancellationToken ct)
        => Ok(await service.SetPermissionsAsync(id, request.PermissionCodes, ct));
}
