using Microsoft.AspNetCore.Authorization;
using NguyenHoaiPhong_1681.Middleware;
using NguyenHoaiPhong_1681.Services;

namespace NguyenHoaiPhong_1681.Authorization;

/// <summary>
/// Kiểm tra quyền theo user + role ĐỌC TỪ DB (không tin claim trong token cũ),
/// user không Active / đã xóa / đang khóa tạm → từ chối.
/// </summary>
/// <remarks>PermissionCode có thể là danh sách "a|b" (có một trong các quyền là đủ).</remarks>
public class PermissionHandler(ICurrentUser currentUser) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;

        CurrentUserInfo user;
        try
        {
            user = await currentUser.GetAsync();
        }
        catch (AppException)
        {
            context.Fail();
            return;
        }

        if (!user.CanAct)
        {
            context.Fail();
            return;
        }

        if (await AnyAsync(requirement.PermissionCode))
            context.Succeed(requirement);
        else
            context.Fail();
    }

    private async Task<bool> AnyAsync(string codes)
    {
        foreach (var code in codes.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (await currentUser.HasPermissionAsync(code)) return true;
        return false;
    }
}
