using System.ComponentModel.DataAnnotations;

namespace WorkOps.Api.Contracts.Projects;

public class CreateProjectRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public Guid? ClientId { get; set; }
}
