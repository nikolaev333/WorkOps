using System.ComponentModel.DataAnnotations;
using WorkOps.Api.Models;

namespace WorkOps.Api.Contracts.Organizations;

public class AddMemberRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [EnumDataType(typeof(OrgRole))]
    public OrgRole Role { get; set; }
}
