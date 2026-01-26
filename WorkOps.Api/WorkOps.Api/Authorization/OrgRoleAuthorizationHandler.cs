using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WorkOps.Api.Data;
using WorkOps.Api.Services;

namespace WorkOps.Api.Authorization;

public sealed class OrgRoleAuthorizationHandler : AuthorizationHandler<OrgRoleRequirement>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OrgRoleAuthorizationHandler(AppDbContext db, ICurrentUserService currentUser, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OrgRoleRequirement requirement)
    {
        var orgIdVal = _httpContextAccessor.HttpContext?.GetRouteValue("orgId")?.ToString();
        if (string.IsNullOrEmpty(orgIdVal) || !Guid.TryParse(orgIdVal, out var orgId)) return;

        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return;

        var ct = _httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        var m = await _db.OrganizationMemberships
            .AsNoTracking()
            .Where(x => x.OrganizationId == orgId && x.UserId == userId)
            .Select(x => new { x.Role })
            .FirstOrDefaultAsync(ct);

        if (m == null) return;
        if (requirement.IsSatisfiedBy(m.Role))
            context.Succeed(requirement);
    }
}
