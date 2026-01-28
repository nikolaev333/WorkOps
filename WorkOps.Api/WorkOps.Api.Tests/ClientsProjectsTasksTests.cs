using System.Net;
using System.Net.Http.Json;
using WorkOps.Api.Contracts.Auth;
using WorkOps.Api.Contracts.Clients;
using WorkOps.Api.Contracts.Common;
using WorkOps.Api.Contracts.Organizations;
using WorkOps.Api.Contracts.Projects;
using WorkOps.Api.Contracts.Tasks;
using WorkOps.Api.Models;
using Xunit;

namespace WorkOps.Api.Tests;

public sealed class ClientsProjectsTasksTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ClientsProjectsTasksTests(CustomWebApplicationFactory factory) => _client = factory.CreateClient();

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

    private async Task<OrganizationResponse> CreateOrgAsync(string token)
    {
        SetBearer(token);
        var create = await _client.PostAsJsonAsync("/api/orgs", new CreateOrganizationRequest { Name = $"Org-{Guid.NewGuid():N}" });
        create.EnsureSuccessStatusCode();
        return (await create.Content.ReadFromJsonAsync<OrganizationResponse>())!;
    }

    [Fact]
    public async Task Clients_Member_Can_GET_List_Returns_200()
    {
        var memberEmail = $"m-{Guid.NewGuid():N}@example.com";
        var adminEmail = $"a-{Guid.NewGuid():N}@example.com";

        var adminLogin = await RegisterAndLoginAsync(_client, adminEmail, "Test1234!");
        var org = await CreateOrgAsync(adminLogin.AccessToken);

        var memberLogin = await RegisterAndLoginAsync(_client, memberEmail, "Test1234!");
        SetBearer(adminLogin.AccessToken);
        await _client.PostAsJsonAsync($"/api/orgs/{org.Id}/members", new AddMemberRequest { Email = memberEmail, Role = OrgRole.Member });

        SetBearer(memberLogin.AccessToken);
        var res = await _client.GetAsync($"/api/orgs/{org.Id}/clients");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<ListResponse<ClientResponse>>();
        Assert.NotNull(list);
    }

    [Fact]
    public async Task Clients_Member_Cannot_POST_Returns_403()
    {
        var memberEmail = $"m2-{Guid.NewGuid():N}@example.com";
        var adminEmail = $"a2-{Guid.NewGuid():N}@example.com";

        var adminLogin = await RegisterAndLoginAsync(_client, adminEmail, "Test1234!");
        var org = await CreateOrgAsync(adminLogin.AccessToken);

        var memberLogin = await RegisterAndLoginAsync(_client, memberEmail, "Test1234!");
        SetBearer(adminLogin.AccessToken);
        await _client.PostAsJsonAsync($"/api/orgs/{org.Id}/members", new AddMemberRequest { Email = memberEmail, Role = OrgRole.Member });

        SetBearer(memberLogin.AccessToken);
        var res = await _client.PostAsJsonAsync($"/api/orgs/{org.Id}/clients", new CreateClientRequest { Name = "Client" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Clients_Manager_Can_POST_Returns_201()
    {
        var managerEmail = $"mgr-{Guid.NewGuid():N}@example.com";
        var adminEmail = $"a3-{Guid.NewGuid():N}@example.com";

        var adminLogin = await RegisterAndLoginAsync(_client, adminEmail, "Test1234!");
        var org = await CreateOrgAsync(adminLogin.AccessToken);

        var managerLogin = await RegisterAndLoginAsync(_client, managerEmail, "Test1234!");
        SetBearer(adminLogin.AccessToken);
        await _client.PostAsJsonAsync($"/api/orgs/{org.Id}/members", new AddMemberRequest { Email = managerEmail, Role = OrgRole.Manager });

        SetBearer(managerLogin.AccessToken);
        var res = await _client.PostAsJsonAsync($"/api/orgs/{org.Id}/clients", new CreateClientRequest { Name = "Test Client" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var client = await res.Content.ReadFromJsonAsync<ClientResponse>();
        Assert.NotNull(client);
        Assert.Equal("Test Client", client.Name);
    }

    [Fact]
    public async Task Projects_Member_Can_GET_List_Returns_200()
    {
        var memberEmail = $"m3-{Guid.NewGuid():N}@example.com";
        var adminEmail = $"a4-{Guid.NewGuid():N}@example.com";

        var adminLogin = await RegisterAndLoginAsync(_client, adminEmail, "Test1234!");
        var org = await CreateOrgAsync(adminLogin.AccessToken);

        var memberLogin = await RegisterAndLoginAsync(_client, memberEmail, "Test1234!");
        SetBearer(adminLogin.AccessToken);
        await _client.PostAsJsonAsync($"/api/orgs/{org.Id}/members", new AddMemberRequest { Email = memberEmail, Role = OrgRole.Member });

        SetBearer(memberLogin.AccessToken);
        var res = await _client.GetAsync($"/api/orgs/{org.Id}/projects");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<ListResponse<ProjectResponse>>();
        Assert.NotNull(list);
    }

    [Fact]
    public async Task Projects_Manager_Can_POST_Returns_201()
    {
        var managerEmail = $"mgr2-{Guid.NewGuid():N}@example.com";
        var adminEmail = $"a5-{Guid.NewGuid():N}@example.com";

        var adminLogin = await RegisterAndLoginAsync(_client, adminEmail, "Test1234!");
        var org = await CreateOrgAsync(adminLogin.AccessToken);

        var managerLogin = await RegisterAndLoginAsync(_client, managerEmail, "Test1234!");
        SetBearer(adminLogin.AccessToken);
        await _client.PostAsJsonAsync($"/api/orgs/{org.Id}/members", new AddMemberRequest { Email = managerEmail, Role = OrgRole.Manager });

        SetBearer(managerLogin.AccessToken);
        var res = await _client.PostAsJsonAsync($"/api/orgs/{org.Id}/projects", new CreateProjectRequest { Name = "Test Project" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var project = await res.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(project);
        Assert.Equal("Test Project", project.Name);
        Assert.False(string.IsNullOrEmpty(project.RowVersion));
    }

    [Fact]
    public async Task Projects_Update_With_Wrong_RowVersion_Returns_409()
    {
        var adminEmail = $"a6-{Guid.NewGuid():N}@example.com";
        var adminLogin = await RegisterAndLoginAsync(_client, adminEmail, "Test1234!");
        var org = await CreateOrgAsync(adminLogin.AccessToken);

        SetBearer(adminLogin.AccessToken);
        var create = await _client.PostAsJsonAsync($"/api/orgs/{org.Id}/projects", new CreateProjectRequest { Name = "Project" });
        create.EnsureSuccessStatusCode();
        var project = (await create.Content.ReadFromJsonAsync<ProjectResponse>())!;

        var wrongVersion = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        var update = await _client.PutAsJsonAsync($"/api/orgs/{org.Id}/projects/{project.Id}", new UpdateProjectRequest
        {
            Name = "Updated",
            Status = ProjectStatus.Active,
            RowVersion = wrongVersion
        });
        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
    }

    [Fact]
    public async Task Tasks_Member_Can_GET_List_Returns_200()
    {
        var memberEmail = $"m4-{Guid.NewGuid():N}@example.com";
        var adminEmail = $"a7-{Guid.NewGuid():N}@example.com";

        var adminLogin = await RegisterAndLoginAsync(_client, adminEmail, "Test1234!");
        var org = await CreateOrgAsync(adminLogin.AccessToken);

        SetBearer(adminLogin.AccessToken);
        var projectRes = await _client.PostAsJsonAsync($"/api/orgs/{org.Id}/projects", new CreateProjectRequest { Name = "Project" });
        projectRes.EnsureSuccessStatusCode();
        var project = (await projectRes.Content.ReadFromJsonAsync<ProjectResponse>())!;

        var memberLogin = await RegisterAndLoginAsync(_client, memberEmail, "Test1234!");
        SetBearer(adminLogin.AccessToken);
        await _client.PostAsJsonAsync($"/api/orgs/{org.Id}/members", new AddMemberRequest { Email = memberEmail, Role = OrgRole.Member });

        SetBearer(memberLogin.AccessToken);
        var res = await _client.GetAsync($"/api/orgs/{org.Id}/projects/{project.Id}/tasks");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<ListResponse<TaskResponse>>();
        Assert.NotNull(list);
    }

    [Fact]
    public async Task Tasks_Manager_Can_POST_Returns_201()
    {
        var managerEmail = $"mgr3-{Guid.NewGuid():N}@example.com";
        var adminEmail = $"a8-{Guid.NewGuid():N}@example.com";

        var adminLogin = await RegisterAndLoginAsync(_client, adminEmail, "Test1234!");
        var org = await CreateOrgAsync(adminLogin.AccessToken);

        SetBearer(adminLogin.AccessToken);
        var projectRes = await _client.PostAsJsonAsync($"/api/orgs/{org.Id}/projects", new CreateProjectRequest { Name = "Project" });
        projectRes.EnsureSuccessStatusCode();
        var project = (await projectRes.Content.ReadFromJsonAsync<ProjectResponse>())!;

        var managerLogin = await RegisterAndLoginAsync(_client, managerEmail, "Test1234!");
        SetBearer(adminLogin.AccessToken);
        await _client.PostAsJsonAsync($"/api/orgs/{org.Id}/members", new AddMemberRequest { Email = managerEmail, Role = OrgRole.Manager });

        SetBearer(managerLogin.AccessToken);
        var res = await _client.PostAsJsonAsync($"/api/orgs/{org.Id}/projects/{project.Id}/tasks", new CreateTaskRequest { Title = "Test Task" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var task = await res.Content.ReadFromJsonAsync<TaskResponse>();
        Assert.NotNull(task);
        Assert.Equal("Test Task", task.Title);
    }

    [Fact]
    public async Task Tasks_Org_Project_Mismatch_Returns_404()
    {
        var adminEmail = $"a9-{Guid.NewGuid():N}@example.com";
        var adminLogin = await RegisterAndLoginAsync(_client, adminEmail, "Test1234!");
        var org1 = await CreateOrgAsync(adminLogin.AccessToken);
        var org2 = await CreateOrgAsync(adminLogin.AccessToken);

        SetBearer(adminLogin.AccessToken);
        var projectRes = await _client.PostAsJsonAsync($"/api/orgs/{org1.Id}/projects", new CreateProjectRequest { Name = "Project" });
        projectRes.EnsureSuccessStatusCode();
        var project = (await projectRes.Content.ReadFromJsonAsync<ProjectResponse>())!;

        var res = await _client.GetAsync($"/api/orgs/{org2.Id}/projects/{project.Id}/tasks");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
