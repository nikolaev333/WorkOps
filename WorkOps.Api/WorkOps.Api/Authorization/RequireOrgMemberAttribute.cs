using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace WorkOps.Api.Authorization;

/// <summary>
/// Returns 404 when the user is not a member of the org (avoids leaking org existence).
/// Runs after role policies (Order &gt; 0) so it can overwrite 403 with 404 when the failure is "not a member".
/// </summary>
public sealed class RequireOrgMemberAttribute : Attribute, IAsyncAuthorizationFilter, IOrderedFilter
{
    public int Order => 1000;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var orgIdVal = context.RouteData.Values["orgId"]?.ToString();
        if (string.IsNullOrEmpty(orgIdVal) || !Guid.TryParse(orgIdVal, out var orgId))
        {
            return;
        }

        var currentUser = context.HttpContext.RequestServices.GetRequiredService<WorkOps.Api.Services.ICurrentUserService>();
        if (!currentUser.IsAuthenticated) return;

        var orgAccess = context.HttpContext.RequestServices.GetRequiredService<WorkOps.Api.Services.IOrgAccessService>();
        if (!await orgAccess.IsMemberAsync(orgId, currentUser.UserId, context.HttpContext.RequestAborted))
            context.Result = new NotFoundResult();
    }
}
