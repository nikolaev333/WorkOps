using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using WorkOps.Api.Authorization;
using WorkOps.Api.Data;
using WorkOps.Api.Models;
using WorkOps.Api.Options;
using WorkOps.Api.Services;

namespace WorkOps.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName()
            .CreateLogger();

        builder.Host.UseSerilog();

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
        builder.Services.AddScoped<IOrgAccessService, OrgAccessService>();

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("OrgMember", p => p.Requirements.Add(new OrgRoleRequirement(OrgRole.Member)));
            options.AddPolicy("OrgManager", p => p.Requirements.Add(new OrgRoleRequirement(OrgRole.Manager)));
            options.AddPolicy("OrgAdmin", p => p.Requirements.Add(new OrgRoleRequirement(OrgRole.Admin)));
        });
        builder.Services.AddScoped<IAuthorizationHandler, OrgRoleAuthorizationHandler>();

        // Rate limiting
        builder.Services.AddRateLimiter(options =>
        {
            // Global policy: 60 requests per minute per IP
            options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(context =>
                System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // Stricter policy for auth endpoints: 10 requests per minute per IP
            options.AddFixedWindowLimiter(policyName: "AuthPolicy", fixedWindowOptions =>
            {
                fixedWindowOptions.PermitLimit = 10;
                fixedWindowOptions.Window = TimeSpan.FromMinutes(1);
                fixedWindowOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
                fixedWindowOptions.QueueLimit = 0;
            });

            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Title = "Too Many Requests",
                    Status = 429,
                    Detail = "Rate limit exceeded. Please try again later."
                }, ct);
            };
        });

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste your JWT access token."
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
            });
        });
        builder.Services.AddProblemDetails();

        builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
        var jwtKey = builder.Configuration["Jwt:Key"];
        if (string.IsNullOrEmpty(jwtKey))
            throw new InvalidOperationException("Jwt:Key not found. Use: dotnet user-secrets set \"Jwt:Key\" \"<min-32-char-secret>\" or set env Jwt__Key.");
        if (jwtKey.Length < 32)
            throw new InvalidOperationException("Jwt:Key must be at least 32 characters for HS256.");

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ClockSkew = TimeSpan.Zero
                };
            });

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not found. Use: dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"<conn>\" or set env ConnectionStrings__DefaultConnection.");

        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        builder.Services.AddIdentityCore<IdentityUser>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredUniqueChars = 4;
        })
            .AddEntityFrameworkStores<AppDbContext>();

        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
            .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

        var app = builder.Build();

        app.UseExceptionHandler(err => err.Run(async ctx =>
        {
            var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
            var exception = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
            logger.LogError(exception, "Unhandled exception occurred");

            ctx.Response.StatusCode = 500;
            ctx.Response.ContentType = "application/problem+json";
            await ctx.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Title = "An error occurred",
                Status = 500,
                Detail = app.Environment.IsDevelopment() && exception != null
                    ? exception.Message
                    : "An internal server error occurred. Please try again later."
            });
        }));

        // Security headers
        app.UseMiddleware<WorkOps.Api.Middleware.SecurityHeadersMiddleware>();

        // Correlation ID middleware (must be early in pipeline)
        app.UseMiddleware<WorkOps.Api.Middleware.CorrelationIdMiddleware>();

        // Serilog request logging (exclude health endpoints)
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            options.GetLevel = (httpContext, elapsed, ex) =>
            {
                // Exclude health endpoints from request logging
                if (httpContext.Request.Path.StartsWithSegments("/health"))
                    return Serilog.Events.LogEventLevel.Verbose; // Effectively disabled

                return ex != null
                    ? Serilog.Events.LogEventLevel.Error
                    : httpContext.Response.StatusCode >= 500
                        ? Serilog.Events.LogEventLevel.Error
                        : httpContext.Response.StatusCode >= 400
                            ? Serilog.Events.LogEventLevel.Warning
                            : Serilog.Events.LogEventLevel.Information;
            };
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                if (httpContext.Items.TryGetValue("CorrelationId", out var correlationId))
                    diagnosticContext.Set("CorrelationId", correlationId);
            };
        });

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
        
        // Skip rate limiting in tests to avoid interference
        if (Environment.GetEnvironmentVariable("DISABLE_RATE_LIMITING") != "true")
        {
            app.UseRateLimiter();
        }
        
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        var opts = new HealthCheckOptions { ResponseWriter = WriteHealthJson };
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = c => c.Tags.Contains("live"), ResponseWriter = opts.ResponseWriter });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready"), ResponseWriter = opts.ResponseWriter });

        app.Run();
    }

    private static Task WriteHealthJson(HttpContext ctx, HealthReport report)
    {
        ctx.Response.ContentType = "application/json";
        var json = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.ToDictionary(e => e.Key, e => new { status = e.Value.Status.ToString(), e.Value.Description })
        });
        return ctx.Response.WriteAsync(json);
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
