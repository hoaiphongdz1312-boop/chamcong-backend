using System.Security.Claims;
using NguyenHoaiPhong_1681.Middleware;

namespace NguyenHoaiPhong_1681.Helpers;

public static class ClaimsExtensions
{
    public const string DepartmentIdClaim = "departmentId";

    /// <summary>UserId từ claim "sub". Không có → 401.</summary>
    public static int GetUserId(this ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue("sub"), out var id)
            ? id
            : throw new AppException(401, "Bạn chưa đăng nhập hoặc phiên đã hết hạn");

    public static string? GetRoleCode(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Role);

    public static int? GetDepartmentId(this ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(DepartmentIdClaim), out var id) ? id : null;
}
