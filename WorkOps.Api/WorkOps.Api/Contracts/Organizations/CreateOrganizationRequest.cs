using System.ComponentModel.DataAnnotations;

namespace WorkOps.Api.Contracts.Organizations;

public class CreateOrganizationRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
}
