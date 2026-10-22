namespace NguyenHoaiPhong_1681.Entities;

public class Role
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Employee = "Employee";

    public int Id { get; set; }
    /// <summary>"Admin" | "Manager" | "Employee" | role tự tạo.</summary>
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>Role hệ thống, không được xóa.</summary>
    public bool IsSystem { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
