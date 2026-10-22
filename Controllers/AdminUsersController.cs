using Microsoft.AspNetCore.Mvc;
using NguyenHoaiPhong_1681.Authorization;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Services;

namespace NguyenHoaiPhong_1681.Controllers;

/// <summary>Quản lý người dùng (Admin).</summary>
[ApiController]
[Route("api/admin/users")]
public class AdminUsersController(IUserAdminService service) : ControllerBase
{
    /// <summary>Danh sách người dùng (tìm kiếm, lọc theo role, trạng thái, phòng ban).</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.UsersView)]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> List([FromQuery] UserListQuery query, CancellationToken ct)
        => Ok(await service.ListAsync(query, ct));

    /// <summary>Danh sách tài khoản chờ duyệt.</summary>
    [HttpGet("pending")]
    [HasPermission(PermissionCodes.UsersView)]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> Pending([FromQuery] PageQuery paging, CancellationToken ct)
        => Ok(await service.ListAsync(new UserListQuery { Status = "PendingApproval", Page = paging.Page, PageSize = paging.PageSize }, ct));

    /// <summary>Chi tiết người dùng.</summary>
    [HttpGet("{id:int}")]
    [HasPermission(PermissionCodes.UsersView)]
    public async Task<ActionResult<AdminUserDto>> Get(int id, CancellationToken ct)
        => Ok(await service.GetAsync(id, ct));

    /// <summary>Tạo tài khoản (Active, email coi như đã xác thực).</summary>
    [HttpPost]
    [HasPermission(PermissionCodes.UsersManage)]
    public async Task<ActionResult<AdminUserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var user = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
    }

    /// <summary>Sửa thông tin người dùng.</summary>
    [HttpPut("{id:int}")]
    [HasPermission(PermissionCodes.UsersManage)]
    public async Task<ActionResult<AdminUserDto>> Update(int id, UpdateUserRequest request, CancellationToken ct)
        => Ok(await service.UpdateAsync(id, request, ct));

    /// <summary>Duyệt tài khoản đang chờ.</summary>
    [HttpPatch("{id:int}/approve")]
    [HasPermission(PermissionCodes.UsersManage)]
    public async Task<ActionResult<AdminUserDto>> Approve(int id, CancellationToken ct)
        => Ok(await service.ApproveAsync(id, ct));

    /// <summary>Khóa tài khoản (thu hồi mọi phiên đăng nhập).</summary>
    [HttpPatch("{id:int}/lock")]
    [HasPermission(PermissionCodes.UsersManage)]
    public async Task<ActionResult<AdminUserDto>> Lock(int id, CancellationToken ct)
        => Ok(await service.LockAsync(id, ct));

    /// <summary>Mở khóa tài khoản (kể cả khóa tạm do sai mật khẩu).</summary>
    [HttpPatch("{id:int}/unlock")]
    [HasPermission(PermissionCodes.UsersManage)]
    public async Task<ActionResult<AdminUserDto>> Unlock(int id, CancellationToken ct)
        => Ok(await service.UnlockAsync(id, ct));

    /// <summary>Đổi vai trò.</summary>
    [HttpPatch("{id:int}/role")]
    [HasPermission(PermissionCodes.UsersManage)]
    public async Task<ActionResult<AdminUserDto>> ChangeRole(int id, ChangeRoleRequest request, CancellationToken ct)
        => Ok(await service.ChangeRoleAsync(id, request.RoleCode, ct));

    /// <summary>Đặt lại mật khẩu (thu hồi mọi phiên đăng nhập).</summary>
    [HttpPatch("{id:int}/reset-password")]
    [HasPermission(PermissionCodes.UsersManage)]
    public async Task<ActionResult<MessageResponse>> ResetPassword(int id, AdminResetPasswordRequest request, CancellationToken ct)
    {
        await service.ResetPasswordAsync(id, request.NewPassword, ct);
        return Ok(new MessageResponse("Đã đặt lại mật khẩu"));
    }

    /// <summary>Xóa mềm người dùng.</summary>
    [HttpDelete("{id:int}")]
    [HasPermission(PermissionCodes.UsersManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
