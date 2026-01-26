using WorkOps.Api.Models;

namespace WorkOps.Api.Services;

public interface IOrgAccessService
{
    Task<bool> IsMemberAsync(Guid orgId, string userId, CancellationToken ct = default);
    Task<bool> IsInRoleAsync(Guid orgId, string userId, CancellationToken ct = default, params OrgRole[] roles);
}
