namespace WorkOps.Api.Contracts.Auth;

public class LoginResponse
{
    public string AccessToken { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
}
