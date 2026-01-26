using Microsoft.EntityFrameworkCore;
using WorkOps.Api.Data;
using WorkOps.Api.Models;

namespace WorkOps.Api.Services;

public sealed class OrgAccessService : IOrgAccessService
{
    private readonly AppDbContext _db;

    public OrgAccessService(AppDbContext db) => _db = db;

    public async Task<bool> IsMemberAsync(Guid orgId, string userId, CancellationToken ct = default)
    {
        return await _db.OrganizationMemberships
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == orgId && m.UserId == userId, ct);
    }

    public async Task<bool> IsInRoleAsync(Guid orgId, string userId, CancellationToken ct = default, params OrgRole[] roles)
    {
        if (roles.Length == 0) return await IsMemberAsync(orgId, userId, ct);
        return await _db.OrganizationMemberships
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == orgId && m.UserId == userId && roles.Contains(m.Role), ct);
    }
}
