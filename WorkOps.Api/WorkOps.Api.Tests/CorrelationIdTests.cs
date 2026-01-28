using System.Net;
using Xunit;

namespace WorkOps.Api.Tests;

public sealed class CorrelationIdTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CorrelationIdTests(CustomWebApplicationFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Response_Includes_CorrelationId_Header()
    {
        var res = await _client.GetAsync("/health/live");
        res.EnsureSuccessStatusCode();
        Assert.True(res.Headers.Contains("X-Correlation-Id"));
        var correlationId = res.Headers.GetValues("X-Correlation-Id").FirstOrDefault();
        Assert.NotNull(correlationId);
        Assert.NotEmpty(correlationId);
    }

    [Fact]
    public async Task Request_With_Custom_CorrelationId_Uses_It()
    {
        var customId = Guid.NewGuid().ToString();
        _client.DefaultRequestHeaders.Add("X-Correlation-Id", customId);
        var res = await _client.GetAsync("/health/live");
        res.EnsureSuccessStatusCode();
        var responseId = res.Headers.GetValues("X-Correlation-Id").FirstOrDefault();
        Assert.Equal(customId, responseId);
    }
}
