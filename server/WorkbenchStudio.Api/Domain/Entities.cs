namespace WorkbenchStudio.Api.Domain;

public enum ImportStatus
{
    Queued,
    Preparing,
    Extracting,
    Inventorying,
    Parsing,
    Validating,
    Indexing,
    Completed,
    CompletedWithWarnings,
    Failed,
    Cancelled
}

public enum ArtifactParseStatus
{
    Pending,
    Parsed,
    ParsedWithWarnings,
    Unsupported,
    Failed,
    Skipped
}

public enum FindingSeverity
{
    Info,
    Warning,
    Error
}

public enum ArtifactReviewStatus
{
    Unreviewed,
    InReview,
    Accepted,
    NeedsAttention
}

public sealed class ProjectEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public ICollection<ImportSnapshotEntity> Imports { get; set; } = new List<ImportSnapshotEntity>();
}

public sealed class ImportSnapshotEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public ProjectEntity? Project { get; set; }
    public required string DisplayName { get; set; }
    public ImportStatus Status { get; set; }
    public string CurrentStage { get; set; } = "Queued";
    public string? StatusMessage { get; set; }
    public string? StagingPath { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
    public long TotalBytes { get; set; }
    public bool CancellationRequested { get; set; }
    public ICollection<ArtifactEntity> Artifacts { get; set; } = new List<ArtifactEntity>();
}

public sealed class ArtifactEntity
{
    public Guid Id { get; set; }
    public Guid ImportSnapshotId { get; set; }
    public ImportSnapshotEntity? ImportSnapshot { get; set; }
    public Guid? ParentArtifactId { get; set; }
    public ArtifactEntity? ParentArtifact { get; set; }
    public required string Name { get; set; }
    public required string RelativePath { get; set; }
    public required string StoragePath { get; set; }
    public required string Extension { get; set; }
    public required string MediaType { get; set; }
    public long SizeBytes { get; set; }
    public required string Sha256 { get; set; }
    public ArtifactParseStatus ParseStatus { get; set; }
    public string? ParserId { get; set; }
    public string? ParserVersion { get; set; }
    public string? StructureSummaryJson { get; set; }
    public string? PreviewText { get; set; }
    public string? ParseError { get; set; }
    public DateTimeOffset ImportedAtUtc { get; set; }
    public ICollection<FindingEntity> Findings { get; set; } = new List<FindingEntity>();
    public ArtifactReviewEntity? Review { get; set; }
}

public sealed class FindingEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ImportSnapshotId { get; set; }
    public Guid? ArtifactId { get; set; }
    public ArtifactEntity? Artifact { get; set; }
    public FindingSeverity Severity { get; set; }
    public required string RuleId { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public string? SourceLocation { get; set; }
    public string? EvidenceExcerpt { get; set; }
    public string? Recommendation { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class ExportRecordEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ImportSnapshotId { get; set; }
    public required string Format { get; set; }
    public required string FileName { get; set; }
    public required string StoragePath { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}


public sealed class ArtifactReviewEntity
{
    public Guid ArtifactId { get; set; }
    public ArtifactEntity? Artifact { get; set; }
    public ArtifactReviewStatus Status { get; set; } = ArtifactReviewStatus.Unreviewed;
    public string? Note { get; set; }
    public string? TagsJson { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public enum WatchTriggerMode
{
    Manual,
    Hourly,
    Daily
}

public enum PlaybookRunStatus
{
    Ready,
    Running,
    Completed,
    CompletedWithWarnings,
    Failed
}

public sealed class WatchFolderEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public required string Name { get; set; }
    public required string FolderPath { get; set; }
    public bool Enabled { get; set; } = true;
    public WatchTriggerMode TriggerMode { get; set; } = WatchTriggerMode.Manual;
    public int ScanIntervalMinutes { get; set; } = 60;
    public string IgnorePatternsJson { get; set; } = "[]";
    public bool RequireApproval { get; set; }
    public string? LastFingerprint { get; set; }
    public DateTimeOffset? LastScannedAtUtc { get; set; }
    public Guid? LastImportId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class DataProfileEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ImportSnapshotId { get; set; }
    public Guid ArtifactId { get; set; }
    public required string ProfileType { get; set; }
    public required string MetricsJson { get; set; }
    public required string IssuesJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class LineageEdgeEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ImportSnapshotId { get; set; }
    public Guid FromArtifactId { get; set; }
    public Guid? ToArtifactId { get; set; }
    public required string EdgeType { get; set; }
    public required string Label { get; set; }
    public string? EvidenceJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class PrivacyDetectionEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ImportSnapshotId { get; set; }
    public Guid ArtifactId { get; set; }
    public required string Kind { get; set; }
    public required string Severity { get; set; }
    public required string SourceLocation { get; set; }
    public required string MaskedPreview { get; set; }
    public string Status { get; set; } = "Open";
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class PlaybookEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string StepsJson { get; set; }
    public PlaybookRunStatus Status { get; set; } = PlaybookRunStatus.Ready;
    public int ProgressPercent { get; set; }
    public string? LastRunSummary { get; set; }
    public DateTimeOffset? LastRunAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public enum BaselineEvaluationStatus
{
    NotRun,
    Passed,
    Improved,
    Regressed,
    NeedsApproval
}

public enum AutomationRecipeStatus
{
    Ready,
    Running,
    Completed,
    CompletedWithWarnings,
    Failed
}

public sealed class BaselinePolicyEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public required string Name { get; set; }
    public Guid BaselineImportId { get; set; }
    public required string RulesJson { get; set; }
    public BaselineEvaluationStatus Status { get; set; } = BaselineEvaluationStatus.NotRun;
    public Guid? LastEvaluatedImportId { get; set; }
    public string? LastResultJson { get; set; }
    public DateTimeOffset? LastEvaluatedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class AutomationRecipeEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string StepsJson { get; set; }
    public bool Enabled { get; set; } = true;
    public string TriggerMode { get; set; } = "Manual";
    public int ScheduleIntervalMinutes { get; set; } = 1440;
    public AutomationRecipeStatus Status { get; set; } = AutomationRecipeStatus.Ready;
    public int ProgressPercent { get; set; }
    public string? LastRunSummary { get; set; }
    public DateTimeOffset? LastRunAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
