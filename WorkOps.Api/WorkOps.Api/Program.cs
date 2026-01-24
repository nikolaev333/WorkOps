using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorkOps.Api.Data;
using WorkOps.Api.Models;

namespace WorkOps.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddProblemDetails();

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found. Set environment variable 'ConnectionStrings__DefaultConnection'.");
        
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        var app = builder.Build();

        app.UseExceptionHandler(err => err.Run(async ctx =>
        {
            ctx.Response.StatusCode = 500;
            ctx.Response.ContentType = "application/problem+json";
            await ctx.Response.WriteAsJsonAsync(new ProblemDetails { Title = "An error occurred", Status = 500 });
        }));

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
            SeedIfEmpty(app);
        }
        else
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseAuthorization();
        app.MapControllers();

        app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "WorkOps.Api" }))
            .WithName("Health")
            .WithTags("Health");

        app.Run();
    }

    private static void SeedIfEmpty(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (db.Projects.Any()) return;
            var now = DateTime.UtcNow;
            db.Projects.AddRange(
                new Project { Id = Guid.NewGuid(), Name = "Sample Project A", CreatedAtUtc = now },
                new Project { Id = Guid.NewGuid(), Name = "Sample Project B", CreatedAtUtc = now });
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(ex, "Dev seed skipped (DB may be unavailable).");
        }
    }
}
