using System.ComponentModel.DataAnnotations;

namespace NguyenHoaiPhong_1681.DTOs;

public class UserListQuery : PageQuery
{
    /// <summary>Tìm theo tên đăng nhập, họ tên, email, số điện thoại.</summary>
    public string? Search { get; set; }
    public string? RoleCode { get; set; }
    /// <summary>PendingApproval | Active | Locked</summary>
    public string? Status { get; set; }
    public int? DepartmentId { get; set; }
}

public record AdminUserDto(
    int Id,
    string Username,
    string FullName,
    string Email,
    bool EmailVerified,
    string? PhoneNumber,
    bool PhoneVerified,
    string? AvatarUrl,
    string Role,
    string Status,
    int? DepartmentId,
    string? DepartmentName,
    DateTime? LockoutUntil,
    DateTime? LastLoginAt,
    DateTime CreatedAt);

public class CreateUserRequest
{
    [Required(ErrorMessage = "Tên đăng nhập là bắt buộc")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải từ 3 đến 50 ký tự")]
    [RegularExpression(@"^[a-zA-Z0-9._]+$", ErrorMessage = "Tên đăng nhập chỉ gồm chữ không dấu, số, dấu chấm và gạch dưới")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(256, ErrorMessage = "Email tối đa 256 ký tự")]
    public string Email { get; set; } = "";

    public string? Phone { get; set; }

    [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự")]
    [RegularExpression(ValidationRules.PasswordPattern, ErrorMessage = ValidationRules.PasswordMessage)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Họ tên là bắt buộc")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Họ tên tối đa 100 ký tự")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Vai trò là bắt buộc")]
    public string RoleCode { get; set; } = "";

    public int? DepartmentId { get; set; }
}

public class UpdateUserRequest
{
    [Required(ErrorMessage = "Họ tên là bắt buộc")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Họ tên tối đa 100 ký tự")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(256, ErrorMessage = "Email tối đa 256 ký tự")]
    public string Email { get; set; } = "";

    public string? Phone { get; set; }

    public int? DepartmentId { get; set; }

    [StringLength(500, ErrorMessage = "Đường dẫn ảnh tối đa 500 ký tự")]
    [Url(ErrorMessage = "Đường dẫn ảnh không hợp lệ")]
    public string? AvatarUrl { get; set; }
}

public class ChangeRoleRequest
{
    [Required(ErrorMessage = "Vai trò là bắt buộc")]
    public string RoleCode { get; set; } = "";
}

public class AdminResetPasswordRequest
{
    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự")]
    [RegularExpression(ValidationRules.PasswordPattern, ErrorMessage = ValidationRules.PasswordMessage)]
    public string NewPassword { get; set; } = "";
}

public record PermissionDto(int Id, string Code, string Name, string Group);

public record RoleDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    bool IsSystem,
    int UserCount,
    IReadOnlyList<string> Permissions);

public class CreateRoleRequest
{
    [Required(ErrorMessage = "Mã vai trò là bắt buộc")]
    [RegularExpression(@"^[A-Za-z][A-Za-z0-9_]{1,49}$", ErrorMessage = "Mã vai trò 2–50 ký tự, bắt đầu bằng chữ, chỉ gồm chữ không dấu, số và gạch dưới")]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Tên vai trò là bắt buộc")]
    [StringLength(100, ErrorMessage = "Tên vai trò tối đa 100 ký tự")]
    public string Name { get; set; } = "";

    [StringLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự")]
    public string? Description { get; set; }
}

public class UpdateRoleRequest
{
    [Required(ErrorMessage = "Tên vai trò là bắt buộc")]
    [StringLength(100, ErrorMessage = "Tên vai trò tối đa 100 ký tự")]
    public string Name { get; set; } = "";

    [StringLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự")]
    public string? Description { get; set; }
}

public class SetRolePermissionsRequest
{
    [Required(ErrorMessage = "Danh sách quyền là bắt buộc")]
    public List<string> PermissionCodes { get; set; } = [];
}
