using System.Text.Json;
using WorkbenchStudio.Api.Domain;

namespace WorkbenchStudio.Api.Services;

public static class DtoMapper
{
    public static ProjectSummaryDto ToDto(ProjectEntity project, ImportSnapshotEntity? latestImport = null) =>
        new(project.Id, project.Name, project.CreatedAtUtc, project.UpdatedAtUtc, project.Imports.Count,
            latestImport?.Id, latestImport?.Status.ToString());

    public static ImportSummaryDto ToDto(ImportSnapshotEntity import) =>
        new(import.Id, import.ProjectId, import.DisplayName, import.Status.ToString(), import.CurrentStage,
            import.StatusMessage, import.CreatedAtUtc, import.StartedAtUtc, import.CompletedAtUtc,
            import.TotalFiles, import.ProcessedFiles, import.WarningCount, import.ErrorCount,
            import.TotalBytes, import.CancellationRequested);

    public static ArtifactListItemDto ToDto(ArtifactEntity artifact, int findingCount = 0) =>
        new(artifact.Id, artifact.ImportSnapshotId, artifact.ParentArtifactId, artifact.Name,
            artifact.RelativePath, artifact.Extension, artifact.MediaType, artifact.SizeBytes,
            artifact.Sha256, artifact.ParseStatus.ToString(), artifact.ParserId, artifact.ParserVersion,
            artifact.ImportedAtUtc, findingCount, artifact.Review?.Status.ToString() ?? ArtifactReviewStatus.Unreviewed.ToString(),
            artifact.Review?.UpdatedAtUtc);

    public static ArtifactReviewDto ToDto(ArtifactReviewEntity? review)
    {
        if (review is null)
        {
            return new ArtifactReviewDto(ArtifactReviewStatus.Unreviewed.ToString(), null, [], null);
        }

        IReadOnlyList<string> tags = [];
        if (!string.IsNullOrWhiteSpace(review.TagsJson))
        {
            try
            {
                tags = JsonSerializer.Deserialize<string[]>(review.TagsJson) ?? [];
            }
            catch (JsonException)
            {
                tags = [];
            }
        }

        return new ArtifactReviewDto(review.Status.ToString(), review.Note, tags, review.UpdatedAtUtc);
    }

    public static FindingDto ToDto(FindingEntity finding, string? artifactPath = null) =>
        new(finding.Id, finding.ImportSnapshotId, finding.ArtifactId,
            artifactPath ?? finding.Artifact?.RelativePath, finding.Severity.ToString(), finding.RuleId,
            finding.Title, finding.Message, finding.SourceLocation, finding.EvidenceExcerpt,
            finding.Recommendation, finding.CreatedAtUtc);

    public static JsonElement? ParseJsonElement(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
