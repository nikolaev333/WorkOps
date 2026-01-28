using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkOps.Api.Contracts.Common;
using WorkOps.Api.Contracts.Tasks;
using WorkOps.Api.Data;
using WorkOps.Api.Models;
using WorkOps.Api.Services;

namespace WorkOps.Api.Controllers;

[ApiController]
[Route("api/orgs/{orgId:guid}/projects/{projectId:guid}/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IOrgAccessService _orgAccess;
    private readonly IAuthorizationService _authz;

    public TasksController(AppDbContext db, ICurrentUserService currentUser, IOrgAccessService orgAccess, IAuthorizationService authz)
    {
        _db = db;
        _currentUser = currentUser;
        _orgAccess = orgAccess;
        _authz = authz;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ListResponse<TaskResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ListResponse<TaskResponse>>> List(
        Guid orgId,
        Guid projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] TaskStatus? status = null,
        [FromQuery] string? assigneeId = null,
        CancellationToken ct = default)
    {
        if (!await _orgAccess.IsMemberAsync(orgId, _currentUser.UserId, ct))
            return NotFound();

        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.OrganizationId == orgId && p.Id == projectId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(ct);
        if (project == default)
            return NotFound();

        page = page > 0 ? page : 1;
        pageSize = pageSize > 0 ? (pageSize > 100 ? 100 : pageSize) : 20;

        var query = _db.Tasks
            .AsNoTracking()
            .Where(t => t.OrganizationId == orgId && t.ProjectId == projectId);

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(assigneeId))
            query = query.Where(t => t.AssigneeUserId == assigneeId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TaskResponse
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status,
                Priority = t.Priority,
                AssigneeUserId = t.AssigneeUserId,
                DueDateUtc = t.DueDateUtc,
                CreatedAtUtc = t.CreatedAtUtc,
                UpdatedAtUtc = t.UpdatedAtUtc
            })
            .ToListAsync(ct);

        return Ok(new ListResponse<TaskResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> Get(Guid orgId, Guid projectId, Guid id, CancellationToken ct)
    {
        if (!await _orgAccess.IsMemberAsync(orgId, _currentUser.UserId, ct))
            return NotFound();

        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.OrganizationId == orgId && p.Id == projectId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(ct);
        if (project == default)
            return NotFound();

        var task = await _db.Tasks
            .AsNoTracking()
            .Where(t => t.OrganizationId == orgId && t.ProjectId == projectId && t.Id == id)
            .Select(t => new TaskResponse
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status,
                Priority = t.Priority,
                AssigneeUserId = t.AssigneeUserId,
                DueDateUtc = t.DueDateUtc,
                CreatedAtUtc = t.CreatedAtUtc,
                UpdatedAtUtc = t.UpdatedAtUtc
            })
            .FirstOrDefaultAsync(ct);

        if (task == null) return NotFound();
        return Ok(task);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(Guid orgId, Guid projectId, [FromBody] CreateTaskRequest req, CancellationToken ct)
    {
        if (!await _orgAccess.IsMemberAsync(orgId, _currentUser.UserId, ct))
            return NotFound();

        var auth = await _authz.AuthorizeAsync(User, (object?)null, "OrgManager");
        if (!auth.Succeeded) return Forbid();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.OrganizationId == orgId && p.Id == projectId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(ct);
        if (project == default)
            return NotFound(new ProblemDetails { Title = "Not found", Status = 404, Detail = "Project not found or does not belong to this organization." });

        var title = (req.Title ?? "").Trim();
        if (title.Length == 0)
            return BadRequest(new ProblemDetails { Title = "Validation failed", Status = 400, Detail = "Title is required." });
        if (title.Length > 200)
            return BadRequest(new ProblemDetails { Title = "Validation failed", Status = 400, Detail = "Title must be at most 200 characters." });

        if (req.DueDateUtc.HasValue && req.DueDateUtc.Value < DateTime.UtcNow)
            return BadRequest(new ProblemDetails { Title = "Validation failed", Status = 400, Detail = "DueDateUtc must be in the future." });

        if (!string.IsNullOrWhiteSpace(req.AssigneeUserId))
        {
            var isMember = await _orgAccess.IsMemberAsync(orgId, req.AssigneeUserId, ct);
            if (!isMember)
                return BadRequest(new ProblemDetails { Title = "Validation failed", Status = 400, Detail = "Assignee must be a member of this organization." });
        }

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            ProjectId = projectId,
            Title = title,
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            Status = TaskStatus.Todo,
            Priority = req.Priority,
            AssigneeUserId = string.IsNullOrWhiteSpace(req.AssigneeUserId) ? null : req.AssigneeUserId,
            DueDateUtc = req.DueDateUtc,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.Tasks.Add(task);
        await _db.SaveChangesAsync(ct);

        return Created($"/api/orgs/{orgId}/projects/{projectId}/tasks/{task.Id}", new TaskResponse
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            Priority = task.Priority,
            AssigneeUserId = task.AssigneeUserId,
            DueDateUtc = task.DueDateUtc,
            CreatedAtUtc = task.CreatedAtUtc,
            UpdatedAtUtc = task.UpdatedAtUtc
        });
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid orgId, Guid projectId, Guid id, [FromBody] UpdateTaskRequest req, CancellationToken ct)
    {
        if (!await _orgAccess.IsMemberAsync(orgId, _currentUser.UserId, ct))
            return NotFound();

        var auth = await _authz.AuthorizeAsync(User, (object?)null, "OrgManager");
        if (!auth.Succeeded) return Forbid();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.OrganizationId == orgId && p.Id == projectId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(ct);
        if (project == default)
            return NotFound();

        var task = await _db.Tasks
            .FirstOrDefaultAsync(t => t.OrganizationId == orgId && t.ProjectId == projectId && t.Id == id, ct);
        if (task == null) return NotFound();

        var title = (req.Title ?? "").Trim();
        if (title.Length == 0)
            return BadRequest(new ProblemDetails { Title = "Validation failed", Status = 400, Detail = "Title is required." });
        if (title.Length > 200)
            return BadRequest(new ProblemDetails { Title = "Validation failed", Status = 400, Detail = "Title must be at most 200 characters." });

        if (!string.IsNullOrWhiteSpace(req.AssigneeUserId))
        {
            var isMember = await _orgAccess.IsMemberAsync(orgId, req.AssigneeUserId, ct);
            if (!isMember)
                return BadRequest(new ProblemDetails { Title = "Validation failed", Status = 400, Detail = "Assignee must be a member of this organization." });
        }

        task.Title = title;
        task.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        task.Status = req.Status;
        task.Priority = req.Priority;
        task.AssigneeUserId = string.IsNullOrWhiteSpace(req.AssigneeUserId) ? null : req.AssigneeUserId;
        task.DueDateUtc = req.DueDateUtc;
        task.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid orgId, Guid projectId, Guid id, CancellationToken ct)
    {
        if (!await _orgAccess.IsMemberAsync(orgId, _currentUser.UserId, ct))
            return NotFound();

        var auth = await _authz.AuthorizeAsync(User, (object?)null, "OrgManager");
        if (!auth.Succeeded) return Forbid();

        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.OrganizationId == orgId && p.Id == projectId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(ct);
        if (project == default)
            return NotFound();

        var task = await _db.Tasks
            .FirstOrDefaultAsync(t => t.OrganizationId == orgId && t.ProjectId == projectId && t.Id == id, ct);
        if (task == null) return NotFound();

        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}
