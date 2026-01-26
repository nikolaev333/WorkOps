using Microsoft.AspNetCore.Authorization;
using WorkOps.Api.Models;

namespace WorkOps.Api.Authorization;

public sealed class OrgRoleRequirement : IAuthorizationRequirement
{
    public OrgRole MinRole { get; }
    public OrgRole[] AllowedRoles { get; }

    public OrgRoleRequirement(OrgRole minRole)
    {
        MinRole = minRole;
        AllowedRoles = Array.Empty<OrgRole>();
    }

    public OrgRoleRequirement(params OrgRole[] allowedRoles)
    {
        MinRole = default;
        AllowedRoles = allowedRoles;
    }

    public bool IsSatisfiedBy(OrgRole role)
    {
        if (AllowedRoles.Length > 0)
            return AllowedRoles.Contains(role);
        return role <= MinRole;
    }
}
