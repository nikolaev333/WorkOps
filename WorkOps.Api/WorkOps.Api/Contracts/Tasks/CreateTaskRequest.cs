using System.ComponentModel.DataAnnotations;
using WorkOps.Api.Models;

namespace WorkOps.Api.Contracts.Tasks;

public class CreateTaskRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public string? AssigneeUserId { get; set; }

    public DateTime? DueDateUtc { get; set; }
}
