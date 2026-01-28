using System.ComponentModel.DataAnnotations;
using WorkOps.Api.Models;

namespace WorkOps.Api.Contracts.Projects;

public class UpdateProjectRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public ProjectStatus Status { get; set; }

    public Guid? ClientId { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}
