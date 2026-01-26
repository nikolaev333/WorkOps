using Microsoft.EntityFrameworkCore;

namespace WorkOps.Api.Data;

/// <summary>
/// For org-scoped routes: always filter by OrganizationId (and Id when loading one entity).
/// Example: /api/orgs/{orgId}/projects, /api/orgs/{orgId}/projects/{projectId}/tasks
/// </summary>
public static class OrgScopeExtensions
{
    public static IQueryable<T> InOrg<T>(this IQueryable<T> query, Guid orgId) where T : IOrgScopedEntity =>
        query.Where(x => x.OrganizationId == orgId);

    public static IQueryable<T> InOrgAndId<T>(this IQueryable<T> query, Guid orgId, Guid id) where T : IOrgScopedEntity =>
        query.Where(x => x.OrganizationId == orgId && x.Id == id);
}
