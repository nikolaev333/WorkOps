using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkOps.Api.Contracts.Common;
using WorkOps.Api.Contracts.Projects;
using WorkOps.Api.Data;
using WorkOps.Api.Models;
using WorkOps.Api.Services;

namespace WorkOps.Api.Controllers;

[ApiController]
[Route("api/orgs/{orgId:guid}/projects")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IOrgAccessService _orgAccess;
    private readonly IAuthorizationService _authz;

    public ProjectsController(AppDbContext db, ICurrentUserService currentUser, IOrgAccessService orgAccess, IAuthorizationService authz)
    {
        _db = db;
        _currentUser = currentUser;
        _orgAccess = orgAccess;
        _authz = authz;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ListResponse<ProjectResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ListResponse<ProjectResponse>>> List(
        Guid orgId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ProjectStatus? status = null,
        [FromQuery] Guid? clientId = null,
        [FromQuery] string? q = null,
        CancellationToken ct = default)
    {
        if (!await _orgAccess.IsMemberAsync(orgId, _currentUser.UserId, ct))
            return NotFound();

        page = page > 0 ? page : 1;
        pageSize = pageSize > 0 ? (pageSize > 100 ? 100 : pageSize) : 20;

        var query = _db.Projects
            .AsNoTracking()
            .Where(p => p.OrganizationId == orgId);

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        if (clientId.HasValue)
            query = query.Where(p => p.ClientId == clientId.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim();
            query = query.Where(p => p.Name.Contains(search));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProjectResponse
            {
                Id = p.Id,
                Name = p.Name,
                Status = p.Status,
                ClientId = p.ClientId,
                CreatedAtUtc = p.CreatedAtUtc,
                UpdatedAtUtc = p.UpdatedAtUtc,
                CreatedByUserId = p.CreatedByUserId,
                RowVersion = p.RowVersion != null && p.RowVersion.Length > 0
                    ? Convert.ToBase64String(p.RowVersion)
                    : string.Empty
            })
            .ToListAsync(ct);

        return Ok(new ListResponse<ProjectResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> Get(Guid orgId, Guid id, CancellationToken ct)
    {
        if (!await _orgAccess.IsMemberAsync(orgId, _currentUser.UserId, ct))
            return NotFound();

        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.OrganizationId == orgId && p.Id == id)
            .Select(p => new ProjectResponse
            {
                Id = p.Id,
                Name = p.Name,
                Status = p.Status,
                ClientId = p.ClientId,
                CreatedAtUtc = p.CreatedAtUtc,
                UpdatedAtUtc = p.UpdatedAtUtc,
                CreatedByUserId = p.CreatedByUserId,
                RowVersion = p.RowVersion != null && p.RowVersion.Length > 0
                    ? Convert.ToBase64String(p.RowVersion)
                    : string.Empty
            })
            .FirstOrDefaultAsync(ct);

        if (project == null) return NotFound();
        return Ok(project);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(Guid orgId, [FromBody] CreateProjectRequest req, CancellationToken ct)
    {
        if (!await _orgAccess.IsMemberAsync(orgId, _currentUser.UserId, ct))
            return NotFound();

        var auth = await _authz.AuthorizeAsync(User, (object?)null, "OrgManager");
        if (!auth.Succeeded) return Forbid();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var name = (req.Name ?? "").Trim();
        if (name.Length == 0)
            return BadRequest(new ProblemDetails { Title = "Validation failed", Status = 400, Detail = "Name is required." });
        if (name.Length > 200)
            return BadRequest(new ProblemDetails { Title = "Validation failed", Status = 400, Detail = "Name must be at most 200 characters." });

        if (req.ClientId.HasValue)
        {
            var clientExists = await _db.Clients
                .AnyAsync(c => c.OrganizationId == orgId && c.Id == req.ClientId.Value, ct);
            if (!clientExists)
                return BadRequest(new ProblemDetails { Title = "Validation failed", Status = 400, Detail = "Client not found or does not belong to this organization." });
        }

        var exists = await _db.Projects
            .AnyAsync(p => p.OrganizationId == orgId && p.Name == name, ct);
        if (exists)
            return Conflict(new ProblemDetails { Title = "Conflict", Status = 409, Detail = "A project with this name already exists in this organization." });

        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            ClientId = req.ClientId,
            Name = name,
            Status = ProjectStatus.Active,
            CreatedByUserId = _currentUser.UserId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync(ct);

        await _db.Entry(project).ReloadAsync(ct);
        var rowVersion = project.RowVersion != null && project.RowVersion.Length > 0
            ? Convert.ToBase64String(project.RowVersion)
            : string.Empty;

        return Created($"/api/orgs/{orgId}/projects/{project.Id}", new ProjectResponse
        {
            Id = project.Id,
            Name = project.Name,
            Status = project.Status,
            ClientId = project.ClientId,
            CreatedAtUtc = project.CreatedAtUtc,
            UpdatedAtUtc = project.UpdatedAtUtc,
            CreatedByUserId = project.CreatedByUserId,
            RowVersion = rowVersion
        });
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid orgId, Guid id, [FromBody] UpdateProjectRequest req, CancellationToken ct)
    {
        if (!await _orgAccess.IsMemberAsync(orgId, _currentUser.UserId, ct))
            return NotFound();

        var auth = await _authz.AuthorizeAsync(User, (object?)null, "OrgManager");
        if (!auth.Succeeded) return Forbid();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(req.RowVersion))
            return BadRequest(new ProblemDetails { Title = "Validation failed", Status = 400, Detail = "RowVersion is required for concurrency control." });

        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.Id == id, ct);
        if (project == null) return NotFound();

        byte[] expectedRowVersion;
        try
        {
            expectedRowVersion = Convert.FromBase64String(req.RowVersion);
        }
        catch
        {
            return BadRequest(new ProblemDetails { Title = "Validation failed", Status = 400, Detail = "Invalid RowVersion format." });
        }

        if (!project.RowVersion.SequenceEqual(expectedRowVersion))
            return Conflict(new ProblemDetails { Title = "Conflict", Status = 409, Detail = "The project was updated by someone else. Please refresh and try again." });

        var name = (req.Name ?? "").Trim();
        if (name.Length == 0)
            return BadRequest(new ProblemDetails { Title = "Validation failed", Status = 400, Detail = "Name is required." });
        if (name.Length > 200)
            return BadRequest(new ProblemDetails { Title = "Validation failed", Status = 400, Detail = "Name must be at most 200 characters." });

        if (req.ClientId.HasValue)
        {
            var clientExists = await _db.Clients
                .AnyAsync(c => c.OrganizationId == orgId && c.Id == req.ClientId.Value, ct);
            if (!clientExists)
                return BadRequest(new ProblemDetails { Title = "Validation failed", Status = 400, Detail = "Client not found or does not belong to this organization." });
        }

        if (project.Name != name)
        {
            var exists = await _db.Projects
                .AnyAsync(p => p.OrganizationId == orgId && p.Name == name && p.Id != id, ct);
            if (exists)
                return Conflict(new ProblemDetails { Title = "Conflict", Status = 409, Detail = "A project with this name already exists in this organization." });
        }

        project.Name = name;
        project.Status = req.Status;
        project.ClientId = req.ClientId;
        project.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid orgId, Guid id, CancellationToken ct)
    {
        if (!await _orgAccess.IsMemberAsync(orgId, _currentUser.UserId, ct))
            return NotFound();

        var auth = await _authz.AuthorizeAsync(User, (object?)null, "OrgManager");
        if (!auth.Succeeded) return Forbid();

        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.Id == id, ct);
        if (project == null) return NotFound();

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}
