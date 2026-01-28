using System.Net;
using System.Text.Json;
using Xunit;

namespace WorkOps.Api.Tests;

public sealed class HealthAndProjectsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthAndProjectsTests(CustomWebApplicationFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task HealthLive_Returns200_And_Healthy()
    {
        var res = await _client.GetAsync("/health/live");
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task HealthReady_Returns200_And_Healthy()
    {
        var res = await _client.GetAsync("/health/ready");
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
    }

}
