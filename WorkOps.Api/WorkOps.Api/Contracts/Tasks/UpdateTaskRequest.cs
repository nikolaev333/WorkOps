using System.ComponentModel.DataAnnotations;
using WorkOps.Api.Models;

namespace WorkOps.Api.Contracts.Tasks;

public class UpdateTaskRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    [Required]
    public Models.TaskStatus Status { get; set; }

    [Required]
    public TaskPriority Priority { get; set; }

    public string? AssigneeUserId { get; set; }

    public DateTime? DueDateUtc { get; set; }
}
