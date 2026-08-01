using System.Text.Json;

namespace WorkbenchStudio.Api.Domain;

public sealed record CreateProjectRequest(string Name);
public sealed record UpdateProjectRequest(string Name);
public sealed record CompareImportsRequest(Guid LeftImportId, Guid RightImportId);
public sealed record UpdateArtifactReviewRequest(string Status, string? Note, IReadOnlyList<string>? Tags);

public sealed record ProjectSummaryDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int ImportCount,
    Guid? LatestImportId,
    string? LatestImportStatus);

public sealed record ImportSummaryDto(
    Guid Id,
    Guid ProjectId,
    string DisplayName,
    string Status,
    string CurrentStage,
    string? StatusMessage,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    int TotalFiles,
    int ProcessedFiles,
    int WarningCount,
    int ErrorCount,
    long TotalBytes,
    bool CancellationRequested);

public sealed record ArtifactListItemDto(
    Guid Id,
    Guid ImportSnapshotId,
    Guid? ParentArtifactId,
    string Name,
    string RelativePath,
    string Extension,
    string MediaType,
    long SizeBytes,
    string Sha256,
    string ParseStatus,
    string? ParserId,
    string? ParserVersion,
    DateTimeOffset ImportedAtUtc,
    int FindingCount,
    string ReviewStatus,
    DateTimeOffset? ReviewUpdatedAtUtc);

public sealed record ArtifactReviewDto(
    string Status,
    string? Note,
    IReadOnlyList<string> Tags,
    DateTimeOffset? UpdatedAtUtc);

public sealed record ArtifactDetailDto(
    ArtifactListItemDto Artifact,
    JsonElement? StructureSummary,
    string? PreviewText,
    string? ParseError,
    IReadOnlyList<FindingDto> Findings,
    ArtifactReviewDto Review);

public sealed record FindingDto(
    Guid Id,
    Guid ImportSnapshotId,
    Guid? ArtifactId,
    string? ArtifactPath,
    string Severity,
    string RuleId,
    string Title,
    string Message,
    string? SourceLocation,
    string? EvidenceExcerpt,
    string? Recommendation,
    DateTimeOffset CreatedAtUtc);

public sealed record CompareResultDto(
    Guid LeftImportId,
    Guid RightImportId,
    int AddedCount,
    int RemovedCount,
    int ModifiedCount,
    int UnchangedCount,
    IReadOnlyList<ArtifactDifferenceDto> Differences);

public sealed record ArtifactDifferenceDto(
    string RelativePath,
    string ChangeType,
    string? LeftSha256,
    string? RightSha256,
    long? LeftSizeBytes,
    long? RightSizeBytes);

public sealed record SearchResultDto(
    IReadOnlyList<ArtifactListItemDto> Artifacts,
    IReadOnlyList<FindingDto> Findings);

public sealed record AgentStatusDto(
    string Status,
    string Service,
    string Version,
    DateTimeOffset TimestampUtc,
    long UptimeSeconds,
    long WorkspaceFreeBytes,
    long WorkspaceTotalBytes,
    long DatabaseSizeBytes,
    int ProjectCount,
    int ImportCount,
    int ArtifactCount,
    int FindingCount,
    int QueuedImportCount,
    IReadOnlyList<string> Parsers,
    WorkspaceLimitsDto Limits);

public sealed record WorkspaceLimitsDto(
    long MaximumUploadBytes,
    long MaximumSingleFileBytes,
    long MaximumExtractedBytes,
    int MaximumExtractedFiles,
    double MaximumCompressionRatio);
