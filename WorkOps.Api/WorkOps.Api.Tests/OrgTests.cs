using System.Net;
using System.Net.Http.Json;
using WorkOps.Api.Contracts.Auth;
using WorkOps.Api.Contracts.Organizations;
using WorkOps.Api.Models;
using Xunit;

namespace WorkOps.Api.Tests;

public sealed class OrgTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OrgTests(CustomWebApplicationFactory factory) => _client = factory.CreateClient();

    private static async Task<LoginResponse> RegisterAndLoginAsync(HttpClient client, string email, string password)
    {
        var reg = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });
        reg.EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    private void SetBearer(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task CreateOrg_Authenticated_Returns_201_And_Creator_Is_Admin()
    {
        var email = $"admin-{Guid.NewGuid():N}@example.com";
        var login = await RegisterAndLoginAsync(_client, email, "Test1234!");
        SetBearer(login.AccessToken);

        var create = await _client.PostAsJsonAsync("/api/orgs", new CreateOrganizationRequest { Name = "Acme" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var org = await create.Content.ReadFromJsonAsync<OrganizationResponse>();
        Assert.NotNull(org);
        Assert.NotEqual(Guid.Empty, org.Id);
        Assert.Equal("Acme", org.Name);

        var members = await _client.GetAsync($"/api/orgs/{org.Id}/members");
        members.EnsureSuccessStatusCode();
        var list = await members.Content.ReadFromJsonAsync<List<MemberResponse>>();
        Assert.NotNull(list);
        var creator = list.Single(m => m.Email == email);
        Assert.Equal(OrgRole.Admin, creator.Role);
    }

    [Fact]
    public async Task ListOrgs_Returns_Only_Orgs_For_That_User()
    {
        var adminEmail = $"a-{Guid.NewGuid():N}@example.com";
        var memberEmail = $"m-{Guid.NewGuid():N}@example.com";
        var outsiderEmail = $"o-{Guid.NewGuid():N}@example.com";

        var adminLogin = await RegisterAndLoginAsync(_client, adminEmail, "Test1234!");
        SetBearer(adminLogin.AccessToken);
        var create = await _client.PostAsJsonAsync("/api/orgs", new CreateOrganizationRequest { Name = "Only Admin's Org" });
        create.EnsureSuccessStatusCode();
        var org = (await create.Content.ReadFromJsonAsync<OrganizationResponse>())!;

        var memberLogin = await RegisterAndLoginAsync(_client, memberEmail, "Test1234!");
        var add = await _client.PostAsJsonAsync($"/api/orgs/{org.Id}/members", new AddMemberRequest { Email = memberEmail, Role = OrgRole.Member });
        add.EnsureSuccessStatusCode();

        SetBearer(memberLogin.AccessToken);
        var listMember = await _client.GetAsync("/api/orgs");
        listMember.EnsureSuccessStatusCode();
        var listM = await listMember.Content.ReadFromJsonAsync<List<OrganizationResponse>>();
        Assert.NotNull(listM);
        Assert.Single(listM);
        Assert.Equal(org.Id, listM[0].Id);

        var outsiderLogin = await RegisterAndLoginAsync(_client, outsiderEmail, "Test1234!");
        SetBearer(outsiderLogin.AccessToken);
        var listOut = await _client.GetAsync("/api/orgs");
        listOut.EnsureSuccessStatusCode();
        var listO = await listOut.Content.ReadFromJsonAsync<List<OrganizationResponse>>();
        Assert.NotNull(listO);
        Assert.Empty(listO);
    }

    [Fact]
    public async Task AddMember_Member_Calls_POST_Returns_403()
    {
        var adminEmail = $"a2-{Guid.NewGuid():N}@example.com";
        var memberEmail = $"m2-{Guid.NewGuid():N}@example.com";
        var newEmail = $"n2-{Guid.NewGuid():N}@example.com";

        var adminLogin = await RegisterAndLoginAsync(_client, adminEmail, "Test1234!");
        SetBearer(adminLogin.AccessToken);
        var create = await _client.PostAsJsonAsync("/api/orgs", new CreateOrganizationRequest { Name = "Org" });
        create.EnsureSuccessStatusCode();
        var org = (await create.Content.ReadFromJsonAsync<OrganizationResponse>())!;

        var memberLogin = await RegisterAndLoginAsync(_client, memberEmail, "Test1234!");
        SetBearer(adminLogin.AccessToken);
        await _client.PostAsJsonAsync($"/api/orgs/{org.Id}/members", new AddMemberRequest { Email = memberEmail, Role = OrgRole.Member });

        await RegisterAndLoginAsync(_client, newEmail, "Test1234!");
        SetBearer(memberLogin.AccessToken);
        var res = await _client.PostAsJsonAsync($"/api/orgs/{org.Id}/members", new AddMemberRequest { Email = newEmail, Role = OrgRole.Member });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task AddMember_Admin_Calls_POST_Returns_201()
    {
        var adminEmail = $"a3-{Guid.NewGuid():N}@example.com";
        var newEmail = $"n3-{Guid.NewGuid():N}@example.com";

        var adminLogin = await RegisterAndLoginAsync(_client, adminEmail, "Test1234!");
        SetBearer(adminLogin.AccessToken);
        var create = await _client.PostAsJsonAsync("/api/orgs", new CreateOrganizationRequest { Name = "Org" });
        create.EnsureSuccessStatusCode();
        var org = (await create.Content.ReadFromJsonAsync<OrganizationResponse>())!;

        await RegisterAndLoginAsync(_client, newEmail, "Test1234!");
        var res = await _client.PostAsJsonAsync($"/api/orgs/{org.Id}/members", new AddMemberRequest { Email = newEmail, Role = OrgRole.Member });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task GetMembers_User_Not_In_Org_Returns_404()
    {
        var adminEmail = $"a4-{Guid.NewGuid():N}@example.com";
        var outsiderEmail = $"o4-{Guid.NewGuid():N}@example.com";

        var adminLogin = await RegisterAndLoginAsync(_client, adminEmail, "Test1234!");
        SetBearer(adminLogin.AccessToken);
        var create = await _client.PostAsJsonAsync("/api/orgs", new CreateOrganizationRequest { Name = "Org" });
        create.EnsureSuccessStatusCode();
        var org = (await create.Content.ReadFromJsonAsync<OrganizationResponse>())!;

        var outsiderLogin = await RegisterAndLoginAsync(_client, outsiderEmail, "Test1234!");
        SetBearer(outsiderLogin.AccessToken);
        var res = await _client.GetAsync($"/api/orgs/{org.Id}/members");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
