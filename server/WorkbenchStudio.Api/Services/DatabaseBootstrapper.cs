using Microsoft.EntityFrameworkCore;
using WorkbenchStudio.Api.Data;

namespace WorkbenchStudio.Api.Services;

public static class DatabaseBootstrapper
{
    public static async Task InitializeAsync(IDbContextFactory<WorkbenchDbContext> factory, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS ArtifactReviews (
                ArtifactId TEXT NOT NULL CONSTRAINT PK_ArtifactReviews PRIMARY KEY,
                Status INTEGER NOT NULL,
                Note TEXT NULL,
                TagsJson TEXT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                CONSTRAINT FK_ArtifactReviews_Artifacts_ArtifactId
                    FOREIGN KEY (ArtifactId) REFERENCES Artifacts (Id) ON DELETE CASCADE
            );
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_ArtifactReviews_Status_UpdatedAtUtc ON ArtifactReviews (Status, UpdatedAtUtc);",
            cancellationToken);
    }
}
