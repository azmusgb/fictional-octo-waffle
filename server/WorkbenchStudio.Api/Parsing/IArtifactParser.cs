using WorkbenchStudio.Api.Domain;

namespace WorkbenchStudio.Api.Parsing;

public sealed record ArtifactParseContext(
    string FilePath,
    string RelativePath,
    string Extension,
    long SizeBytes);

public sealed record ParserFinding(
    FindingSeverity Severity,
    string RuleId,
    string Title,
    string Message,
    string? SourceLocation = null,
    string? EvidenceExcerpt = null,
    string? Recommendation = null);

public sealed record ArtifactParseResult(
    ArtifactParseStatus Status,
    string ParserId,
    string ParserVersion,
    object? StructureSummary,
    string? PreviewText,
    IReadOnlyList<ParserFinding> Findings,
    string? Error = null);

public interface IArtifactParser
{
    string ParserId { get; }
    string ParserVersion { get; }
    bool CanParse(ArtifactParseContext context);
    Task<ArtifactParseResult> ParseAsync(ArtifactParseContext context, CancellationToken cancellationToken);
}
