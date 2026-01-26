using WorkOps.Api.Models;

namespace WorkOps.Api.Contracts.Organizations;

public class MemberResponse
{
    public string UserId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public OrgRole Role { get; set; }
    public DateTime JoinedAtUtc { get; set; }
}
