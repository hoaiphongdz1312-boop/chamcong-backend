using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace NguyenHoaiPhong_1681.Authorization;

public record PermissionRequirement(string PermissionCode) : IAuthorizationRequirement;

/// <summary>Sinh policy "perm:{code}" động thay vì khai báo từng policy.</summary>
public class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public const string PolicyPrefix = "perm:";

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PolicyPrefix, StringComparison.Ordinal))
            return await base.GetPolicyAsync(policyName);

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName[PolicyPrefix.Length..]))
            .Build();
    }
}
