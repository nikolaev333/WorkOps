using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkOps.Api.Contracts.Projects;
using WorkOps.Api.Data;
using WorkOps.Api.Models;

namespace WorkOps.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProjectsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<ProjectResponse>>> List(CancellationToken ct)
    {
        var list = await _db.Projects
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new ProjectResponse { Id = p.Id, Name = p.Name, CreatedAtUtc = p.CreatedAtUtc })
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectResponse>> Get(Guid id, CancellationToken ct)
    {
        var p = await _db.Projects
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new ProjectResponse { Id = x.Id, Name = x.Name, CreatedAtUtc = x.CreatedAtUtc })
            .FirstOrDefaultAsync(ct);
        if (p == null) return NotFound();
        return Ok(p);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request, CancellationToken ct)
    {
        var entity = new Project { Id = Guid.NewGuid(), Name = request.Name.Trim(), CreatedAtUtc = DateTime.UtcNow };
        _db.Projects.Add(entity);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, ToResponse(entity));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectRequest request, CancellationToken ct)
    {
        var entity = await _db.Projects.FindAsync([id], ct);
        if (entity == null) return NotFound();
        entity.Name = request.Name.Trim();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entity = await _db.Projects.FindAsync([id], ct);
        if (entity == null) return NotFound();
        _db.Projects.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static ProjectResponse ToResponse(Project p) => new() { Id = p.Id, Name = p.Name, CreatedAtUtc = p.CreatedAtUtc };
}
