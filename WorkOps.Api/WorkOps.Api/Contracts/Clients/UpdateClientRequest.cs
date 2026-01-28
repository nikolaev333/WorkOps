using System.ComponentModel.DataAnnotations;

namespace WorkOps.Api.Contracts.Clients;

public class UpdateClientRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(320)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }
}
