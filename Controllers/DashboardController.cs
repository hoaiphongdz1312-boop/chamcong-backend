using Microsoft.AspNetCore.Mvc;
using NguyenHoaiPhong_1681.Authorization;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Services;

namespace NguyenHoaiPhong_1681.Controllers;

/// <summary>Trang tổng quan theo vai trò.</summary>
[ApiController]
[Route("api/dashboard")]
public class DashboardController(IDashboardService service) : ControllerBase
{
    /// <summary>Tổng quan quản trị.</summary>
    [HttpGet("admin")]
    [HasPermission(PermissionCodes.DashboardAdmin)]
    public async Task<ActionResult<AdminDashboardDto>> Admin(CancellationToken ct) => Ok(await service.AdminAsync(ct));

    /// <summary>Tổng quan quản lý (phòng mình quản lý).</summary>
    [HttpGet("manager")]
    [HasPermission(PermissionCodes.DashboardManager)]
    public async Task<ActionResult<ManagerDashboardDto>> Manager(CancellationToken ct) => Ok(await service.ManagerAsync(ct));

    /// <summary>Tổng quan nhân viên.</summary>
    [HttpGet("employee")]
    [HasPermission(PermissionCodes.DashboardEmployee)]
    public async Task<ActionResult<EmployeeDashboardDto>> Employee(CancellationToken ct) => Ok(await service.EmployeeAsync(ct));
}
