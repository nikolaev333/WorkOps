using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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

        // ConfigureTestServices runs after the app's ConfigureServices, so we can replace
        // the SQL Server DbContext from Program with InMemory (no SQL in CI).
        builder.ConfigureTestServices(services =>
        {
            var opts = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (opts != null) services.Remove(opts);
            var ctx = services.SingleOrDefault(d => d.ServiceType == typeof(AppDbContext));
            if (ctx != null) services.Remove(ctx);
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("TestDb"));
        });
    }
}
