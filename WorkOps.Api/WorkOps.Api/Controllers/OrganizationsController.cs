using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkOps.Api.Contracts.Organizations;
using WorkOps.Api.Data;
using WorkOps.Api.Models;
using WorkOps.Api.Services;

namespace WorkOps.Api.Controllers;

[ApiController]
[Route("api/orgs")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public OrganizationsController(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateOrganizationRequest req, CancellationToken ct)
    {
        var name = (req.Name ?? "").Trim();
        if (name.Length == 0)
            return BadRequest(new ProblemDetails { Title = "Validation failed", Status = 400, Detail = "Name is required." });
        if (name.Length > 200)
            return BadRequest(new ProblemDetails { Title = "Validation failed", Status = 400, Detail = "Name must be at most 200 characters." });

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Organizations.Add(org);
        _db.OrganizationMemberships.Add(new OrganizationMembership
        {
            OrganizationId = org.Id,
            UserId = _currentUser.UserId,
            Role = OrgRole.Admin,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        return Created($"/api/orgs/{org.Id}", new OrganizationResponse
        {
            Id = org.Id,
            Name = org.Name,
            CreatedAtUtc = org.CreatedAtUtc
        });
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<OrganizationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OrganizationResponse>>> List(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        var list = await _db.OrganizationMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Join(_db.Organizations.AsNoTracking(), m => m.OrganizationId, o => o.Id, (m, o) => o)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Select(o => new OrganizationResponse { Id = o.Id, Name = o.Name, CreatedAtUtc = o.CreatedAtUtc })
            .ToListAsync(ct);
        return Ok(list);
    }
}
