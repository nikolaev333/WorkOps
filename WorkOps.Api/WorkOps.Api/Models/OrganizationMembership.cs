namespace WorkOps.Api.Models;

public class OrganizationMembership
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;

    public OrgRole Role { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
