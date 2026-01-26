namespace WorkOps.Api.Services;

public interface ICurrentUserService
{
    string UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
}
