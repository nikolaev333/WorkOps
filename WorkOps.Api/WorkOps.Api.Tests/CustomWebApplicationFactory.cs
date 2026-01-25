using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkOps.Api.Data;

namespace WorkOps.Api.Tests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<WorkOps.Api.Program>
{
    public CustomWebApplicationFactory()
    {
        // Ensure connection string exists before Program.Main runs (ConfigureAppConfiguration
        // on IWebHostBuilder is not applied in time with minimal hosting / WebApplication.CreateBuilder).
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection",
            "Server=.;Database=Noop;TrustServerCertificate=True;");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=Noop;TrustServerCertificate=True;"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("TestDb"));
        });
    }
}
