using Microsoft.EntityFrameworkCore;

namespace TestShieldAI.Api.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProjectRecord> Projects => Set<ProjectRecord>();

    public DbSet<OpenApiSpecificationRecord> OpenApiSpecifications => Set<OpenApiSpecificationRecord>();

    public DbSet<ProjectBaselineRecord> ProjectBaselines => Set<ProjectBaselineRecord>();

    public DbSet<ProjectTestRunRecord> ProjectTestRuns => Set<ProjectTestRunRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var project = modelBuilder.Entity<ProjectRecord>();
        project.ToTable("Projects");
        project.HasKey(entity => entity.Id);
        project.Property(entity => entity.Name).IsRequired();
        project.Property(entity => entity.BaseUrl).IsRequired();
        project.Property(entity => entity.CreatedAt).IsRequired();
        project.HasMany(entity => entity.Specifications)
            .WithOne(entity => entity.Project)
            .HasForeignKey(entity => entity.ProjectId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        project.HasOne(entity => entity.Baseline)
            .WithOne(entity => entity.Project)
            .HasForeignKey<ProjectBaselineRecord>(entity => entity.ProjectId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        project.HasOne(entity => entity.LastRun)
            .WithOne(entity => entity.Project)
            .HasForeignKey<ProjectTestRunRecord>(entity => entity.ProjectId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        var spec = modelBuilder.Entity<OpenApiSpecificationRecord>();
        spec.ToTable("OpenApiSpecifications");
        spec.HasKey(entity => entity.Id);
        spec.Property(entity => entity.ProjectId).IsRequired();
        spec.Property(entity => entity.RawSpecification).IsRequired();
        spec.Property(entity => entity.ImportedAt).IsRequired();

        var baseline = modelBuilder.Entity<ProjectBaselineRecord>();
        baseline.ToTable("ProjectBaselines");
        baseline.HasKey(entity => entity.Id);
        baseline.Property(entity => entity.ProjectId).IsRequired();
        baseline.Property(entity => entity.EstablishedAt).IsRequired();
        baseline.Property(entity => entity.SnapshotJson).IsRequired();
        baseline.HasIndex(entity => entity.ProjectId).IsUnique();

        var lastRun = modelBuilder.Entity<ProjectTestRunRecord>();
        lastRun.ToTable("ProjectTestRuns");
        lastRun.HasKey(entity => entity.Id);
        lastRun.Property(entity => entity.ProjectId).IsRequired();
        lastRun.Property(entity => entity.CompletedAt).IsRequired();
        lastRun.Property(entity => entity.Decision).IsRequired();
        lastRun.Property(entity => entity.PassedCount).IsRequired();
        lastRun.Property(entity => entity.FailedCount).IsRequired();
        lastRun.Property(entity => entity.ErrorCount).IsRequired();
        lastRun.Property(entity => entity.FindingCount).IsRequired();
        lastRun.Property(entity => entity.ResultsJson).IsRequired();
        lastRun.HasIndex(entity => entity.ProjectId).IsUnique();
    }
}
