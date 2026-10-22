using NguyenHoaiPhong_1681.Entities;

namespace NguyenHoaiPhong_1681.Helpers;

public static class RoleRoutes
{
    /// <summary>Trang app chuyển tới sau đăng nhập theo role.</summary>
    public static string HomeRoute(string roleCode) => roleCode switch
    {
        Role.Admin => "/admin",
        Role.Manager => "/manager",
        Role.Employee => "/employee/dashboard",
        _ => "/profile"
    };
}
