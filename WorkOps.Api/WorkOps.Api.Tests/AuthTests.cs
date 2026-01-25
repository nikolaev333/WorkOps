using System.Net;
using System.Net.Http.Json;
using WorkOps.Api.Contracts.Auth;
using Xunit;

namespace WorkOps.Api.Tests;

public sealed class AuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthTests(CustomWebApplicationFactory factory) => _client = factory.CreateClient();

    private static async Task<LoginResponse> RegisterAndLoginAsync(HttpClient client, string email, string password)
    {
        var reg = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });
        reg.EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        return body!;
    }

    [Fact]
    public async Task Register_Then_Login_Returns_Token()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var password = "Test1234!";

        var reg = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });
        reg.EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body.AccessToken));
        Assert.True(body.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task Me_Without_Token_Returns_401()
    {
        var res = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Register_With_Mismatched_ConfirmPassword_Returns_400()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var res = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest
            {
                Email = email,
                Password = "Test1234!",
                ConfirmPassword = "Different1234!"
            });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Register_With_Invalid_Email_Returns_400()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest
            {
                Email = "not-an-email",
                Password = "Test1234!",
                ConfirmPassword = "Test1234!"
            });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Register_Same_Email_Twice_Returns_409()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var password = "Test1234!";

        var first = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });
        first.EnsureSuccessStatusCode();

        var second = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_With_Wrong_Password_Returns_401()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var password = "Test1234!";

        var reg = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });
        reg.EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = "WrongPassword!1" });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Login_With_Unknown_User_Returns_401()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = $"missing-{Guid.NewGuid():N}@example.com", Password = "Test1234!" });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Me_With_Invalid_Token_Returns_401()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "this.is.not.a.jwt");

        var me = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Me_With_Token_For_User_Returns_Same_User_Data()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var password = "Test1234!";

        var loginBody = await RegisterAndLoginAsync(_client, email, password);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginBody.AccessToken);

        var me = await _client.GetAsync("/api/auth/me");
        me.EnsureSuccessStatusCode();

        var meBody = await me.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(meBody);
        Assert.Equal(email, meBody.Email);
        Assert.False(string.IsNullOrWhiteSpace(meBody.UserId));
    }


}
