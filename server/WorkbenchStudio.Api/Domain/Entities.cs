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
