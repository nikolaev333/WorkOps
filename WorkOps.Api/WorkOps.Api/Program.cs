using Microsoft.EntityFrameworkCore;
using WorkOps.Api.Data;

namespace WorkOps.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found. Set environment variable 'ConnectionStrings__DefaultConnection'.");
        
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
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

        app.MapGet("/api/projects", async (AppDbContext db) =>
        {
            var projects = await db.Projects.ToListAsync();
            return Results.Ok(projects);
        })
            .WithName("GetProjects")
            .WithTags("Projects");

        app.Run();
    }
}
