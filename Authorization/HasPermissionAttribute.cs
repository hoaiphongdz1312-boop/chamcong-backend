using Microsoft.AspNetCore.Authorization;

namespace NguyenHoaiPhong_1681.Authorization;

/// <summary>Yêu cầu mã quyền (lưu trong DB) để gọi endpoint. Ví dụ: [HasPermission("users.view")].</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class HasPermissionAttribute(string permissionCode)
    : AuthorizeAttribute(PermissionPolicyProvider.PolicyPrefix + permissionCode)
{
    public string PermissionCode { get; } = permissionCode;
}
