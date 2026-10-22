using NguyenHoaiPhong_1681.Entities;

namespace NguyenHoaiPhong_1681.Authorization;

public record PermissionDefinition(string Code, string Name, string Group);

/// <summary>Danh sách mã quyền và quyền mặc định của từng role (dùng khi seed).</summary>
public static class PermissionCodes
{
    public const string UsersView = "users.view";
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";
    public const string DepartmentsView = "departments.view";
    public const string DepartmentsManage = "departments.manage";
    public const string ShiftsView = "shifts.view";
    public const string ShiftsManage = "shifts.manage";
    public const string AttendanceSelf = "attendance.self";
    public const string AttendanceViewTeam = "attendance.view_team";
    public const string AttendanceViewAll = "attendance.view_all";
    public const string AttendanceEdit = "attendance.edit";
    public const string AttendanceDelete = "attendance.delete";
    public const string LeaveSelf = "leave.self";
    public const string LeaveApproveTeam = "leave.approve_team";
    public const string LeaveApproveAll = "leave.approve_all";
    public const string DashboardAdmin = "dashboard.admin";
    public const string DashboardManager = "dashboard.manager";
    public const string DashboardEmployee = "dashboard.employee";
    public const string ProfileSelf = "profile.self";

    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(UsersView, "Xem người dùng", "Người dùng"),
        new(UsersManage, "Quản lý người dùng", "Người dùng"),
        new(RolesManage, "Quản lý vai trò và phân quyền", "Phân quyền"),
        new(DepartmentsView, "Xem phòng ban", "Phòng ban"),
        new(DepartmentsManage, "Quản lý phòng ban", "Phòng ban"),
        new(ShiftsView, "Xem ca làm", "Ca làm"),
        new(ShiftsManage, "Quản lý ca làm", "Ca làm"),
        new(AttendanceSelf, "Chấm công cá nhân", "Chấm công"),
        new(AttendanceViewTeam, "Xem chấm công phòng ban", "Chấm công"),
        new(AttendanceViewAll, "Xem toàn bộ chấm công", "Chấm công"),
        new(AttendanceEdit, "Sửa chấm công", "Chấm công"),
        new(AttendanceDelete, "Xóa chấm công", "Chấm công"),
        new(LeaveSelf, "Xin nghỉ phép", "Nghỉ phép"),
        new(LeaveApproveTeam, "Duyệt nghỉ phép phòng ban", "Nghỉ phép"),
        new(LeaveApproveAll, "Duyệt toàn bộ nghỉ phép", "Nghỉ phép"),
        new(DashboardAdmin, "Trang tổng quan quản trị", "Tổng quan"),
        new(DashboardManager, "Trang tổng quan quản lý", "Tổng quan"),
        new(DashboardEmployee, "Trang tổng quan nhân viên", "Tổng quan"),
        new(ProfileSelf, "Hồ sơ cá nhân", "Hồ sơ"),
    ];

    /// <summary>Quyền mặc định. Admin luôn có mọi quyền nên không liệt kê.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Defaults = new Dictionary<string, string[]>
    {
        [Role.Manager] =
        [
            DepartmentsView, ShiftsView, AttendanceSelf, AttendanceViewTeam, AttendanceEdit,
            LeaveSelf, LeaveApproveTeam, DashboardManager, ProfileSelf
        ],
        [Role.Employee] = [ShiftsView, AttendanceSelf, LeaveSelf, DashboardEmployee, ProfileSelf],
    };
}
