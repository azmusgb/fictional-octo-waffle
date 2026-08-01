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

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS WatchFolders (
                Id TEXT NOT NULL CONSTRAINT PK_WatchFolders PRIMARY KEY,
                ProjectId TEXT NOT NULL,
                Name TEXT NOT NULL,
                FolderPath TEXT NOT NULL,
                Enabled INTEGER NOT NULL,
                TriggerMode INTEGER NOT NULL,
                ScanIntervalMinutes INTEGER NOT NULL,
                IgnorePatternsJson TEXT NOT NULL,
                RequireApproval INTEGER NOT NULL,
                LastFingerprint TEXT NULL,
                LastScannedAtUtc TEXT NULL,
                LastImportId TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_WatchFolders_ProjectId_Enabled_LastScannedAtUtc
                ON WatchFolders (ProjectId, Enabled, LastScannedAtUtc);

            CREATE TABLE IF NOT EXISTS DataProfiles (
                Id TEXT NOT NULL CONSTRAINT PK_DataProfiles PRIMARY KEY,
                ProjectId TEXT NOT NULL,
                ImportSnapshotId TEXT NOT NULL,
                ArtifactId TEXT NOT NULL,
                ProfileType TEXT NOT NULL,
                MetricsJson TEXT NOT NULL,
                IssuesJson TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_DataProfiles_ImportSnapshotId_ArtifactId
                ON DataProfiles (ImportSnapshotId, ArtifactId);

            CREATE TABLE IF NOT EXISTS LineageEdges (
                Id TEXT NOT NULL CONSTRAINT PK_LineageEdges PRIMARY KEY,
                ProjectId TEXT NOT NULL,
                ImportSnapshotId TEXT NOT NULL,
                FromArtifactId TEXT NOT NULL,
                ToArtifactId TEXT NULL,
                EdgeType TEXT NOT NULL,
                Label TEXT NOT NULL,
                EvidenceJson TEXT NULL,
                CreatedAtUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_LineageEdges_ImportSnapshotId_FromArtifactId_ToArtifactId_EdgeType
                ON LineageEdges (ImportSnapshotId, FromArtifactId, ToArtifactId, EdgeType);

            CREATE TABLE IF NOT EXISTS PrivacyDetections (
                Id TEXT NOT NULL CONSTRAINT PK_PrivacyDetections PRIMARY KEY,
                ProjectId TEXT NOT NULL,
                ImportSnapshotId TEXT NOT NULL,
                ArtifactId TEXT NOT NULL,
                Kind TEXT NOT NULL,
                Severity TEXT NOT NULL,
                SourceLocation TEXT NOT NULL,
                MaskedPreview TEXT NOT NULL,
                Status TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_PrivacyDetections_ImportSnapshotId_ArtifactId_Kind
                ON PrivacyDetections (ImportSnapshotId, ArtifactId, Kind);

            CREATE TABLE IF NOT EXISTS Playbooks (
                Id TEXT NOT NULL CONSTRAINT PK_Playbooks PRIMARY KEY,
                ProjectId TEXT NOT NULL,
                Name TEXT NOT NULL,
                Description TEXT NOT NULL,
                StepsJson TEXT NOT NULL,
                Status INTEGER NOT NULL,
                ProgressPercent INTEGER NOT NULL,
                LastRunSummary TEXT NULL,
                LastRunAtUtc TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Playbooks_ProjectId_UpdatedAtUtc ON Playbooks (ProjectId, UpdatedAtUtc);

            CREATE TABLE IF NOT EXISTS BaselinePolicies (
                Id TEXT NOT NULL CONSTRAINT PK_BaselinePolicies PRIMARY KEY,
                ProjectId TEXT NOT NULL,
                Name TEXT NOT NULL,
                BaselineImportId TEXT NOT NULL,
                RulesJson TEXT NOT NULL,
                Status INTEGER NOT NULL,
                LastEvaluatedImportId TEXT NULL,
                LastResultJson TEXT NULL,
                LastEvaluatedAtUtc TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_BaselinePolicies_ProjectId_UpdatedAtUtc
                ON BaselinePolicies (ProjectId, UpdatedAtUtc);

            CREATE TABLE IF NOT EXISTS AutomationRecipes (
                Id TEXT NOT NULL CONSTRAINT PK_AutomationRecipes PRIMARY KEY,
                ProjectId TEXT NOT NULL,
                Name TEXT NOT NULL,
                Description TEXT NOT NULL,
                StepsJson TEXT NOT NULL,
                Enabled INTEGER NOT NULL,
                TriggerMode TEXT NOT NULL,
                ScheduleIntervalMinutes INTEGER NOT NULL,
                Status INTEGER NOT NULL,
                ProgressPercent INTEGER NOT NULL,
                LastRunSummary TEXT NULL,
                LastRunAtUtc TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_AutomationRecipes_ProjectId_Enabled_UpdatedAtUtc
                ON AutomationRecipes (ProjectId, Enabled, UpdatedAtUtc);


            CREATE TABLE IF NOT EXISTS QueuePolicies (
                Id TEXT NOT NULL CONSTRAINT PK_QueuePolicies PRIMARY KEY,
                ProjectId TEXT NOT NULL,
                Name TEXT NOT NULL,
                WeightsJson TEXT NOT NULL,
                SlaHours INTEGER NOT NULL,
                Active INTEGER NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_QueuePolicies_ProjectId_Active_UpdatedAtUtc
                ON QueuePolicies (ProjectId, Active, UpdatedAtUtc);

            CREATE TABLE IF NOT EXISTS ScenarioRuns (
                Id TEXT NOT NULL CONSTRAINT PK_ScenarioRuns PRIMARY KEY,
                ProjectId TEXT NOT NULL,
                ImportSnapshotId TEXT NOT NULL,
                Name TEXT NOT NULL,
                AssumptionsJson TEXT NOT NULL,
                ResultJson TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_ScenarioRuns_ProjectId_ImportSnapshotId_CreatedAtUtc
                ON ScenarioRuns (ProjectId, ImportSnapshotId, CreatedAtUtc);

            CREATE TABLE IF NOT EXISTS ApprovalGates (
                Id TEXT NOT NULL CONSTRAINT PK_ApprovalGates PRIMARY KEY,
                ProjectId TEXT NOT NULL,
                ImportSnapshotId TEXT NOT NULL,
                Name TEXT NOT NULL,
                GateType TEXT NOT NULL,
                RequiredRole TEXT NOT NULL,
                Status TEXT NOT NULL,
                RequirementsJson TEXT NOT NULL,
                DecidedBy TEXT NULL,
                Rationale TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                DecidedAtUtc TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_ApprovalGates_ProjectId_ImportSnapshotId_Status
                ON ApprovalGates (ProjectId, ImportSnapshotId, Status);
            """, cancellationToken);
    }
}
