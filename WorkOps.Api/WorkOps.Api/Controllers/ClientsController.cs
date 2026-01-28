using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkOps.Api.Contracts.Clients;
using WorkOps.Api.Contracts.Common;
using WorkOps.Api.Data;
using WorkOps.Api.Models;
using WorkOps.Api.Services;

namespace WorkOps.Api.Controllers;

[ApiController]
[Route("api/orgs/{orgId:guid}/clients")]
[Authorize]
public class ClientsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IOrgAccessService _orgAccess;
    private readonly IAuthorizationService _authz;

    public ClientsController(AppDbContext db, ICurrentUserService currentUser, IOrgAccessService orgAccess, IAuthorizationService authz)
    {
        _db = db;
        _currentUser = currentUser;
        _orgAccess = orgAccess;
        _authz = authz;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ListResponse<ClientResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ListResponse<ClientResponse>>> List(
        Guid orgId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null,
        CancellationToken ct = default)
    {
        if (!await _orgAccess.IsMemberAsync(orgId, _currentUser.UserId, ct))
            return NotFound();

        page = page > 0 ? page : 1;
        pageSize = pageSize > 0 ? (pageSize > 100 ? 100 : pageSize) : 20;

        var query = _db.Clients
            .AsNoTracking()
            .Where(c => c.OrganizationId == orgId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim();
            query = query.Where(c => c.Name.Contains(search));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ClientResponse
            {
                Id = c.Id,
                Name = c.Name,
                Email = c.Email,
                Phone = c.Phone,
                CreatedAtUtc = c.CreatedAtUtc,
                UpdatedAtUtc = c.UpdatedAtUtc
            })
            .ToListAsync(ct);

        return Ok(new ListResponse<ClientResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientResponse>> Get(Guid orgId, Guid id, CancellationToken ct)
    {
        if (!await _orgAccess.IsMemberAsync(orgId, _currentUser.UserId, ct))
            return NotFound();

        var client = await _db.Clients
            .AsNoTracking()
            .Where(c => c.OrganizationId == orgId && c.Id == id)
            .Select(c => new ClientResponse
            {
                Id = c.Id,
                Name = c.Name,
                Email = c.Email,
                Phone = c.Phone,
                CreatedAtUtc = c.CreatedAtUtc,
                UpdatedAtUtc = c.UpdatedAtUtc
            })
            .FirstOrDefaultAsync(ct);

        if (client == null) return NotFound();
        return Ok(client);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(Guid orgId, [FromBody] CreateClientRequest req, CancellationToken ct)
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

        var exists = await _db.Clients
            .AnyAsync(c => c.OrganizationId == orgId && c.Name == name, ct);
        if (exists)
            return Conflict(new ProblemDetails { Title = "Conflict", Status = 409, Detail = "A client with this name already exists in this organization." });

        var client = new Client
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Name = name,
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.Clients.Add(client);
        await _db.SaveChangesAsync(ct);

        return Created($"/api/orgs/{orgId}/clients/{client.Id}", new ClientResponse
        {
            Id = client.Id,
            Name = client.Name,
            Email = client.Email,
            Phone = client.Phone,
            CreatedAtUtc = client.CreatedAtUtc,
            UpdatedAtUtc = client.UpdatedAtUtc
        });
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid orgId, Guid id, [FromBody] UpdateClientRequest req, CancellationToken ct)
    {
        if (!await _orgAccess.IsMemberAsync(orgId, _currentUser.UserId, ct))
            return NotFound();

        var auth = await _authz.AuthorizeAsync(User, (object?)null, "OrgManager");
        if (!auth.Succeeded) return Forbid();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var client = await _db.Clients
            .FirstOrDefaultAsync(c => c.OrganizationId == orgId && c.Id == id, ct);
        if (client == null) return NotFound();

        var name = (req.Name ?? "").Trim();
        if (name.Length == 0)
            return BadRequest(new ProblemDetails { Title = "Validation failed", Status = 400, Detail = "Name is required." });
        if (name.Length > 200)
            return BadRequest(new ProblemDetails { Title = "Validation failed", Status = 400, Detail = "Name must be at most 200 characters." });

        if (client.Name != name)
        {
            var exists = await _db.Clients
                .AnyAsync(c => c.OrganizationId == orgId && c.Name == name && c.Id != id, ct);
            if (exists)
                return Conflict(new ProblemDetails { Title = "Conflict", Status = 409, Detail = "A client with this name already exists in this organization." });
        }

        client.Name = name;
        client.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        client.Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
        client.UpdatedAtUtc = DateTime.UtcNow;
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

        var client = await _db.Clients
            .FirstOrDefaultAsync(c => c.OrganizationId == orgId && c.Id == id, ct);
        if (client == null) return NotFound();

        _db.Clients.Remove(client);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}
