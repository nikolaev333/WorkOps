using WorkOps.Api.Models;

namespace WorkOps.Api.Contracts.Projects;

public class ProjectResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProjectStatus Status { get; set; }
    public Guid? ClientId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}
