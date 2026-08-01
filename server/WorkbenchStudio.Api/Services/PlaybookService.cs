using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkbenchStudio.Api.Data;
using WorkbenchStudio.Api.Domain;

namespace WorkbenchStudio.Api.Services;

public sealed class PlaybookService(
    IDbContextFactory<WorkbenchDbContext> factory,
    DataProfileService profiles,
    LineageService lineage,
    PrivacyService privacy)
{
    public async Task<PlaybookDto> RunAsync(Guid playbookId, Guid importId, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var playbook = await db.Playbooks.SingleOrDefaultAsync(x => x.Id == playbookId, cancellationToken)
            ?? throw new KeyNotFoundException("Playbook was not found.");
        var import = await db.Imports.AsNoTracking().SingleOrDefaultAsync(x => x.Id == importId && x.ProjectId == playbook.ProjectId, cancellationToken)
            ?? throw new KeyNotFoundException("Import snapshot was not found.");

        var steps = DeserializeSteps(playbook.StepsJson);
        playbook.Status = PlaybookRunStatus.Running;
        playbook.ProgressPercent = 0;
        playbook.LastRunAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var summaries = new List<string>();
        try
        {
            for (var index = 0; index < steps.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var step = steps[index];
                var type = step.Type.Trim().ToLowerInvariant();
                switch (type)
                {
                    case "profile":
                        summaries.Add($"Profiled {await profiles.ProfileImportAsync(playbook.ProjectId, importId, cancellationToken):N0} artifacts");
                        break;
                    case "lineage":
                    case "impact":
                        summaries.Add($"Built {await lineage.RebuildAsync(playbook.ProjectId, importId, cancellationToken):N0} lineage edges");
                        break;
                    case "privacy":
                    case "privacy-scan":
                        summaries.Add($"Detected {await privacy.ScanAsync(playbook.ProjectId, importId, cancellationToken):N0} sensitive-value candidates");
                        break;
                    case "review":
                        var unreviewed = await db.Artifacts.CountAsync(x => x.ImportSnapshotId == importId && (x.Review == null || x.Review.Status == ArtifactReviewStatus.Unreviewed), cancellationToken);
                        summaries.Add($"Review queue contains {unreviewed:N0} unreviewed artifacts");
                        break;
                    default:
                        summaries.Add($"Prepared step: {step.Name}");
                        break;
                }

                playbook.ProgressPercent = (int)Math.Round((index + 1d) / Math.Max(1, steps.Count) * 100d);
                playbook.LastRunSummary = string.Join(" · ", summaries);
                await db.SaveChangesAsync(cancellationToken);
            }

            playbook.Status = PlaybookRunStatus.Completed;
            playbook.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return ToDto(playbook);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            playbook.Status = PlaybookRunStatus.Failed;
            playbook.LastRunSummary = $"Failed: {exception.Message}";
            playbook.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    public static IReadOnlyList<PlaybookStepDto> DeserializeSteps(string json)
    {
        try { return JsonSerializer.Deserialize<PlaybookStepDto[]>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? []; }
        catch (JsonException) { return []; }
    }

    public static PlaybookDto ToDto(PlaybookEntity entity) => new(
        entity.Id, entity.ProjectId, entity.Name, entity.Description, DeserializeSteps(entity.StepsJson), entity.Status.ToString(),
        entity.ProgressPercent, entity.LastRunSummary, entity.LastRunAtUtc);
}
