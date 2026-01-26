using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkOps.Api.Contracts.Organizations;
using WorkOps.Api.Data;
using WorkOps.Api.Models;
using WorkOps.Api.Services;

namespace WorkOps.Api.Controllers;

[ApiController]
[Route("api/orgs/{orgId:guid}/members")]
[Authorize]
public class OrganizationMembersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IOrgAccessService _orgAccess;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationService _authz;

    public OrganizationMembersController(AppDbContext db, UserManager<IdentityUser> userManager, IOrgAccessService orgAccess, ICurrentUserService currentUser, IAuthorizationService authz)
    {
        _db = db;
        _userManager = userManager;
        _orgAccess = orgAccess;
        _currentUser = currentUser;
        _authz = authz;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<MemberResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<MemberResponse>>> List(Guid orgId, CancellationToken ct)
    {
        if (!await _orgAccess.IsMemberAsync(orgId, _currentUser.UserId, ct)) return NotFound();
        var auth = await _authz.AuthorizeAsync(User, (object?)null, "OrgManager");
        if (!auth.Succeeded) return Forbid();

        var list = await _db.OrganizationMemberships
            .AsNoTracking()
            .Where(m => m.OrganizationId == orgId)
            .Join(_db.Users.AsNoTracking(), m => m.UserId, u => u.Id, (m, u) => new { m, u })
            .OrderBy(x => x.m.CreatedAtUtc)
            .Select(x => new MemberResponse
            {
                UserId = x.m.UserId,
                Email = x.u.Email,
                Role = x.m.Role,
                JoinedAtUtc = x.m.CreatedAtUtc
            })
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpPost]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Add(Guid orgId, [FromBody] AddMemberRequest req, CancellationToken ct)
    {
        if (!await _orgAccess.IsMemberAsync(orgId, _currentUser.UserId, ct)) return NotFound();
        var authAdd = await _authz.AuthorizeAsync(User, (object?)null, "OrgAdmin");
        if (!authAdd.Succeeded) return Forbid();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user == null)
            return BadRequest(new ProblemDetails { Title = "Invalid request", Status = 400, Detail = "User not found." });

        var exists = await _db.OrganizationMemberships
            .AnyAsync(m => m.OrganizationId == orgId && m.UserId == user.Id, ct);
        if (exists)
            return Conflict(new ProblemDetails { Title = "Conflict", Status = 409, Detail = "User is already a member of this organization." });

        var membership = new OrganizationMembership
        {
            OrganizationId = orgId,
            UserId = user.Id,
            Role = req.Role,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.OrganizationMemberships.Add(membership);
        await _db.SaveChangesAsync(ct);

        return Created($"/api/orgs/{orgId}/members/{user.Id}", new MemberResponse
        {
            UserId = user.Id,
            Email = user.Email,
            Role = membership.Role,
            JoinedAtUtc = membership.CreatedAtUtc
        });
    }

    [HttpDelete("{userId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove(Guid orgId, string userId, CancellationToken ct)
    {
        if (!await _orgAccess.IsMemberAsync(orgId, _currentUser.UserId, ct)) return NotFound();
        var authDel = await _authz.AuthorizeAsync(User, (object?)null, "OrgAdmin");
        if (!authDel.Succeeded) return Forbid();

        var membership = await _db.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.OrganizationId == orgId && m.UserId == userId, ct);
        if (membership == null)
            return NotFound(new ProblemDetails { Title = "Not found", Status = 404, Detail = "Membership not found." });

        if (membership.Role == OrgRole.Admin)
        {
            var adminCount = await _db.OrganizationMemberships
                .CountAsync(m => m.OrganizationId == orgId && m.Role == OrgRole.Admin, ct);
            if (adminCount <= 1)
                return Conflict(new ProblemDetails { Title = "Conflict", Status = 409, Detail = "Cannot remove the last admin." });
        }

        _db.OrganizationMemberships.Remove(membership);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
