using Microsoft.EntityFrameworkCore;
using WorkbenchStudio.Api.Domain;

namespace WorkbenchStudio.Api.Data;

public sealed class WorkbenchDbContext(DbContextOptions<WorkbenchDbContext> options) : DbContext(options)
{
    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();
    public DbSet<ImportSnapshotEntity> Imports => Set<ImportSnapshotEntity>();
    public DbSet<ArtifactEntity> Artifacts => Set<ArtifactEntity>();
    public DbSet<FindingEntity> Findings => Set<FindingEntity>();
    public DbSet<ExportRecordEntity> Exports => Set<ExportRecordEntity>();
    public DbSet<ArtifactReviewEntity> ArtifactReviews => Set<ArtifactReviewEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasMany(x => x.Imports)
                .WithOne(x => x.Project)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ImportSnapshotEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DisplayName).HasMaxLength(240).IsRequired();
            entity.Property(x => x.CurrentStage).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.CreatedAtUtc });
            entity.HasMany(x => x.Artifacts)
                .WithOne(x => x.ImportSnapshot)
                .HasForeignKey(x => x.ImportSnapshotId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ArtifactEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(260).IsRequired();
            entity.Property(x => x.RelativePath).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.StoragePath).HasMaxLength(4096).IsRequired();
            entity.Property(x => x.Extension).HasMaxLength(32).IsRequired();
            entity.Property(x => x.MediaType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ParserId).HasMaxLength(120);
            entity.Property(x => x.ParserVersion).HasMaxLength(40);
            entity.HasIndex(x => new { x.ImportSnapshotId, x.RelativePath }).IsUnique();
            entity.HasIndex(x => x.Sha256);
            entity.HasOne(x => x.ParentArtifact)
                .WithMany()
                .HasForeignKey(x => x.ParentArtifactId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FindingEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RuleId).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(240).IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.ImportSnapshotId, x.Severity });
            entity.HasOne(x => x.Artifact)
                .WithMany(x => x.Findings)
                .HasForeignKey(x => x.ArtifactId)
                .OnDelete(DeleteBehavior.Cascade);
        });


        modelBuilder.Entity<ArtifactReviewEntity>(entity =>
        {
            entity.HasKey(x => x.ArtifactId);
            entity.Property(x => x.Note).HasMaxLength(4000);
            entity.Property(x => x.TagsJson).HasMaxLength(2000);
            entity.HasOne(x => x.Artifact)
                .WithOne(x => x.Review)
                .HasForeignKey<ArtifactReviewEntity>(x => x.ArtifactId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.Status, x.UpdatedAtUtc });
        });

        modelBuilder.Entity<ExportRecordEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Format).HasMaxLength(20).IsRequired();
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.StoragePath).HasMaxLength(4096).IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.ImportSnapshotId, x.CreatedAtUtc });
        });
    }
}
