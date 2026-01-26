namespace WorkOps.Api.Data;

/// <summary>
/// Implement on entities that belong to an organization. Use with InOrg / InOrgAndId when querying.
/// </summary>
public interface IOrgScopedEntity
{
    Guid OrganizationId { get; }
    Guid Id { get; }
}
