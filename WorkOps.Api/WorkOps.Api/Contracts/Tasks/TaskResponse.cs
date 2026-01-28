using WorkOps.Api.Models;

namespace WorkOps.Api.Contracts.Tasks;

public class TaskResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public string? AssigneeUserId { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
