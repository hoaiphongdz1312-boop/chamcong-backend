using System.ComponentModel.DataAnnotations;

namespace NguyenHoaiPhong_1681.DTOs;

// ===== Phòng ban =====

public record DepartmentDto(
    int Id,
    string Name,
    string? Description,
    int? ManagerId,
    string? ManagerName,
    int MemberCount,
    DateTime CreatedAt);

public class DepartmentListQuery : PageQuery
{
    public string? Search { get; set; }
}

public class SaveDepartmentRequest
{
    [Required(ErrorMessage = "Tên phòng ban là bắt buộc")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên phòng ban từ 2 đến 100 ký tự")]
    public string Name { get; set; } = "";

    [StringLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự")]
    public string? Description { get; set; }

    /// <summary>User có role Manager (tùy chọn).</summary>
    public int? ManagerId { get; set; }
}

public class SetDepartmentManagerRequest
{
    [Required(ErrorMessage = "userId là bắt buộc")]
    public int? UserId { get; set; }
}

// ===== Ca làm =====

public record ShiftDto(
    int Id,
    string Name,
    string StartTime,
    string EndTime,
    int LateToleranceMinutes,
    bool IsActive,
    DateTime CreatedAt);

public class ShiftListQuery : PageQuery
{
    public bool? ActiveOnly { get; set; }
}

public class SaveShiftRequest
{
    public const string TimePattern = @"^([01]\d|2[0-3]):[0-5]\d$";

    [Required(ErrorMessage = "Tên ca là bắt buộc")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên ca từ 2 đến 100 ký tự")]
    public string Name { get; set; } = "";

    /// <summary>Dạng "HH:mm".</summary>
    [Required(ErrorMessage = "Giờ bắt đầu là bắt buộc")]
    [RegularExpression(TimePattern, ErrorMessage = "Giờ bắt đầu phải có dạng HH:mm")]
    public string StartTime { get; set; } = "";

    /// <summary>Dạng "HH:mm"; nhỏ hơn giờ bắt đầu nghĩa là ca qua đêm.</summary>
    [Required(ErrorMessage = "Giờ kết thúc là bắt buộc")]
    [RegularExpression(TimePattern, ErrorMessage = "Giờ kết thúc phải có dạng HH:mm")]
    public string EndTime { get; set; } = "";

    [Range(0, 120, ErrorMessage = "Số phút cho phép trễ từ 0 đến 120")]
    public int LateToleranceMinutes { get; set; }

    public bool IsActive { get; set; } = true;
}

// ===== Chấm công =====

public record AttendanceDto(
    int Id,
    int UserId,
    string Username,
    string FullName,
    int? DepartmentId,
    string? DepartmentName,
    int ShiftId,
    string ShiftName,
    DateOnly WorkDate,
    DateTime CheckInAt,
    DateTime? CheckOutAt,
    string Status,
    int WorkMinutes,
    string? Note,
    int? EditedById,
    DateTime? EditedAt,
    DateTime CreatedAt,
    double? CheckInLatitude,
    double? CheckInLongitude,
    int? CheckInDistanceMeters,
    double? CheckOutLatitude,
    double? CheckOutLongitude,
    int? CheckOutDistanceMeters);

public class CheckInOutRequest
{
    public int? ShiftId { get; set; }

    [Required(ErrorMessage = "Vĩ độ là bắt buộc")]
    [Range(-90, 90, ErrorMessage = "Vĩ độ phải từ -90 đến 90")]
    public double? Latitude { get; set; }

    [Required(ErrorMessage = "Kinh độ là bắt buộc")]
    [Range(-180, 180, ErrorMessage = "Kinh độ phải từ -180 đến 180")]
    public double? Longitude { get; set; }

    /// <summary>Điện thoại báo vị trí giả (mock location).</summary>
    public bool IsMockLocation { get; set; }
}

public class MyAttendanceQuery : PageQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public class AttendanceQuery : MyAttendanceQuery
{
    public int? UserId { get; set; }
    public int? DepartmentId { get; set; }
    public int? ShiftId { get; set; }
    /// <summary>OnTime | Late | EarlyLeave | LateAndEarlyLeave</summary>
    public string? Status { get; set; }
}

public class UpdateAttendanceRequest
{
    [Required(ErrorMessage = "Giờ check-in là bắt buộc")]
    public DateTime? CheckInAt { get; set; }

    public DateTime? CheckOutAt { get; set; }

    [Required(ErrorMessage = "Trạng thái là bắt buộc")]
    [RegularExpression(@"^(OnTime|Late|EarlyLeave|LateAndEarlyLeave)$", ErrorMessage = "Trạng thái chỉ nhận OnTime, Late, EarlyLeave, LateAndEarlyLeave")]
    public string Status { get; set; } = "";

    [Required(ErrorMessage = "Ghi chú lý do sửa là bắt buộc")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Ghi chú từ 3 đến 500 ký tự")]
    public string Note { get; set; } = "";
}

// ===== Nghỉ phép =====

public record LeaveRequestDto(
    int Id,
    int UserId,
    string Username,
    string FullName,
    int? DepartmentId,
    string? DepartmentName,
    DateOnly FromDate,
    DateOnly ToDate,
    int Days,
    string Reason,
    string Status,
    int? ReviewerId,
    string? ReviewerName,
    string? ReviewNote,
    DateTime? ReviewedAt,
    DateTime CreatedAt);

public class MyLeaveQuery : PageQuery
{
    /// <summary>Pending | Approved | Rejected | Cancelled</summary>
    public string? Status { get; set; }
}

public class LeaveQuery : MyLeaveQuery
{
    public int? DepartmentId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public class SaveLeaveRequest
{
    [Required(ErrorMessage = "Ngày bắt đầu là bắt buộc")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "Ngày kết thúc là bắt buộc")]
    public DateOnly? ToDate { get; set; }

    [Required(ErrorMessage = "Lý do là bắt buộc")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Lý do từ 3 đến 500 ký tự")]
    public string Reason { get; set; } = "";
}

public class ApproveLeaveRequest
{
    [StringLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự")]
    public string? Note { get; set; }
}

public class RejectLeaveRequest
{
    [Required(ErrorMessage = "Lý do từ chối là bắt buộc")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Lý do từ chối từ 3 đến 500 ký tự")]
    public string Note { get; set; } = "";
}

// ===== Dashboard =====

public record CountItem(string Key, string Label, int Count);

public record AdminDashboardDto(
    int TotalUsers,
    int PendingApproval,
    int Locked,
    IReadOnlyList<CountItem> UsersByRole,
    IReadOnlyList<CountItem> UsersByDepartment,
    int AttendanceToday,
    int LateToday,
    int PendingLeaveRequests);

public record ManagerDashboardDto(
    IReadOnlyList<DepartmentDto> Departments,
    int TeamSize,
    int PresentToday,
    int LateToday,
    int OnLeaveToday,
    int AbsentToday,
    int PendingLeaveRequests);

public record EmployeeShiftToday(
    int ShiftId,
    string ShiftName,
    string StartTime,
    string EndTime,
    int? AttendanceId,
    DateTime? CheckInAt,
    DateTime? CheckOutAt,
    string? Status);

public record EmployeeDashboardDto(
    DateOnly Today,
    IReadOnlyList<EmployeeShiftToday> ShiftsToday,
    int WorkDaysThisMonth,
    int LateCountThisMonth,
    int ApprovedLeaveDaysThisMonth,
    int PendingLeaveRequests);

public record DepartmentMemberDto(
    int Id,
    string Username,
    string FullName,
    string Email,
    string? PhoneNumber,
    string Role,
    string Status);

// ===== Báo cáo bảng công tháng =====

public class MonthlyReportQuery
{
    /// <summary>Tháng dạng yyyy-MM.</summary>
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Tháng là bắt buộc")]
    [System.ComponentModel.DataAnnotations.RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "Tháng phải có dạng yyyy-MM")]
    public string Month { get; set; } = "";
    public int? DepartmentId { get; set; }
}

public record MonthlyReportRowDto(
    int UserId,
    string Username,
    string FullName,
    int? DepartmentId,
    string? DepartmentName,
    int WorkDays,
    int LateCount,
    int EarlyLeaveCount,
    int WorkMinutes,
    int LeaveDays,
    int AbsentDays);

public record MonthlyReportDto(
    string Month,
    int EmployeeCount,
    int TotalWorkDays,
    int TotalLateCount,
    int TotalWorkMinutes,
    int TotalLeaveDays,
    int TotalAbsentDays,
    IReadOnlyList<MonthlyReportRowDto> Rows);
