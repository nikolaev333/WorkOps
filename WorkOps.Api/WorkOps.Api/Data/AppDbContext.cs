using Microsoft.EntityFrameworkCore;
using WorkOps.Api.Models;

namespace WorkOps.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Project> Projects { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Project>(entity =>
        {
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(e => e.CreatedAtUtc)
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    public override int SaveChanges()
    {
        SetCreatedAtForNewProjects();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetCreatedAtForNewProjects();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetCreatedAtForNewProjects()
    {
        foreach (var entry in ChangeTracker.Entries<Project>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAtUtc == default)
            {
                entry.Entity.CreatedAtUtc = DateTime.UtcNow;
            }
        }
    }
}
