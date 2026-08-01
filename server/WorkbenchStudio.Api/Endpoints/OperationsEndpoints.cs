using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkbenchStudio.Api.Data;
using WorkbenchStudio.Api.Domain;
using WorkbenchStudio.Api.Services;

namespace WorkbenchStudio.Api.Endpoints;

public static class OperationsEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects/{projectId:guid}").WithTags("Operations");

        group.MapGet("/watch-folders", GetWatchFoldersAsync);
        group.MapPost("/watch-folders", CreateWatchFolderAsync);
        group.MapPatch("/watch-folders/{watchFolderId:guid}", UpdateWatchFolderAsync);
        group.MapPost("/watch-folders/{watchFolderId:guid}/scan", ScanWatchFolderAsync);

        group.MapPost("/imports/{importId:guid}/profiles/run", RunProfilesAsync);
        group.MapGet("/imports/{importId:guid}/profiles", GetProfilesAsync);

        group.MapPost("/imports/{importId:guid}/lineage/rebuild", RebuildLineageAsync);
        group.MapGet("/imports/{importId:guid}/lineage", GetLineageAsync);

        group.MapPost("/imports/{importId:guid}/privacy/scan", RunPrivacyScanAsync);
        group.MapGet("/imports/{importId:guid}/privacy", GetPrivacyDetectionsAsync);
        group.MapPatch("/imports/{importId:guid}/privacy/{detectionId:guid}", UpdatePrivacyDetectionAsync);
        group.MapGet("/imports/{importId:guid}/privacy/redacted-export", CreateRedactedExportAsync);

        group.MapGet("/playbooks", GetPlaybooksAsync);
        group.MapPost("/playbooks", CreatePlaybookAsync);
        group.MapPost("/playbooks/{playbookId:guid}/run/{importId:guid}", RunPlaybookAsync);
        return app;
    }

    private static async Task<IResult> GetWatchFoldersAsync(Guid projectId, IDbContextFactory<WorkbenchDbContext> factory, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var items = await db.WatchFolders.AsNoTracking().Where(x => x.ProjectId == projectId).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return Results.Ok(items.Select(ToDto));
    }

    private static async Task<IResult> CreateWatchFolderAsync(Guid projectId, CreateWatchFolderRequest request, IDbContextFactory<WorkbenchDbContext> factory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.FolderPath))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["watchFolder"] = ["Name and folder path are required."] });
        if (!Enum.TryParse<WatchTriggerMode>(request.TriggerMode, true, out var trigger))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["triggerMode"] = ["Use Manual, Hourly, or Daily."] });

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (!await db.Projects.AnyAsync(x => x.Id == projectId, cancellationToken)) return Results.NotFound();
        var now = DateTimeOffset.UtcNow;
        var entity = new WatchFolderEntity
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Name = request.Name.Trim(), FolderPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(request.FolderPath.Trim())),
            TriggerMode = trigger, ScanIntervalMinutes = trigger == WatchTriggerMode.Daily ? 1440 : Math.Clamp(request.ScanIntervalMinutes ?? 60, 60, 10080),
            IgnorePatternsJson = JsonSerializer.Serialize(request.IgnorePatterns ?? [], JsonOptions), RequireApproval = request.RequireApproval,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.WatchFolders.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/projects/{projectId}/watch-folders/{entity.Id}", ToDto(entity));
    }

    private static async Task<IResult> UpdateWatchFolderAsync(Guid projectId, Guid watchFolderId, UpdateWatchFolderRequest request, IDbContextFactory<WorkbenchDbContext> factory, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var entity = await db.WatchFolders.SingleOrDefaultAsync(x => x.Id == watchFolderId && x.ProjectId == projectId, cancellationToken);
        if (entity is null) return Results.NotFound();
        if (!string.IsNullOrWhiteSpace(request.Name)) entity.Name = request.Name.Trim();
        if (request.Enabled.HasValue) entity.Enabled = request.Enabled.Value;
        if (!string.IsNullOrWhiteSpace(request.TriggerMode) && Enum.TryParse<WatchTriggerMode>(request.TriggerMode, true, out var trigger)) entity.TriggerMode = trigger;
        if (request.ScanIntervalMinutes.HasValue) entity.ScanIntervalMinutes = Math.Clamp(request.ScanIntervalMinutes.Value, 60, 10080);
        if (request.IgnorePatterns is not null) entity.IgnorePatternsJson = JsonSerializer.Serialize(request.IgnorePatterns, JsonOptions);
        if (request.RequireApproval.HasValue) entity.RequireApproval = request.RequireApproval.Value;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(entity));
    }

    private static async Task<IResult> ScanWatchFolderAsync(Guid projectId, Guid watchFolderId, bool? force, WatchFolderService service, CancellationToken cancellationToken)
    {
        try { var result = await service.ScanAsync(watchFolderId, force ?? true, cancellationToken); return Results.Accepted(result.ImportId.HasValue ? $"/api/imports/{result.ImportId}" : null, result); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (DirectoryNotFoundException exception) { return Results.BadRequest(new { error = exception.Message }); }
    }

    private static async Task<IResult> RunProfilesAsync(Guid projectId, Guid importId, DataProfileService service, CancellationToken cancellationToken) =>
        Results.Ok(new { profiledArtifacts = await service.ProfileImportAsync(projectId, importId, cancellationToken) });

    private static async Task<IResult> GetProfilesAsync(Guid projectId, Guid importId, IDbContextFactory<WorkbenchDbContext> factory, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await (from profile in db.DataProfiles.AsNoTracking()
                          join artifact in db.Artifacts.AsNoTracking() on profile.ArtifactId equals artifact.Id
                          where profile.ProjectId == projectId && profile.ImportSnapshotId == importId
                          orderby artifact.RelativePath
                          select new { profile, artifact.RelativePath }).ToListAsync(cancellationToken);
        return Results.Ok(rows.Select(x => new DataProfileDto(x.profile.Id, x.profile.ArtifactId, x.RelativePath, x.profile.ProfileType,
            JsonSerializer.Deserialize<JsonElement>(x.profile.MetricsJson), JsonSerializer.Deserialize<JsonElement>(x.profile.IssuesJson), x.profile.CreatedAtUtc)));
    }

    private static async Task<IResult> RebuildLineageAsync(Guid projectId, Guid importId, LineageService service, CancellationToken cancellationToken) =>
        Results.Ok(new { edges = await service.RebuildAsync(projectId, importId, cancellationToken) });

    private static async Task<IResult> GetLineageAsync(Guid projectId, Guid importId, IDbContextFactory<WorkbenchDbContext> factory, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var artifacts = await db.Artifacts.AsNoTracking().Where(x => x.ImportSnapshotId == importId).ToDictionaryAsync(x => x.Id, x => x.RelativePath, cancellationToken);
        var edges = await db.LineageEdges.AsNoTracking().Where(x => x.ProjectId == projectId && x.ImportSnapshotId == importId).ToListAsync(cancellationToken);
        return Results.Ok(edges.Select(edge => new LineageEdgeDto(edge.Id, edge.FromArtifactId, artifacts.GetValueOrDefault(edge.FromArtifactId, "Unknown"), edge.ToArtifactId,
            edge.ToArtifactId.HasValue ? artifacts.GetValueOrDefault(edge.ToArtifactId.Value) : null, edge.EdgeType, edge.Label,
            string.IsNullOrWhiteSpace(edge.EvidenceJson) ? null : JsonSerializer.Deserialize<JsonElement>(edge.EvidenceJson))));
    }

    private static async Task<IResult> RunPrivacyScanAsync(Guid projectId, Guid importId, PrivacyService service, CancellationToken cancellationToken) =>
        Results.Ok(new { detections = await service.ScanAsync(projectId, importId, cancellationToken) });

    private static async Task<IResult> GetPrivacyDetectionsAsync(Guid projectId, Guid importId, IDbContextFactory<WorkbenchDbContext> factory, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await (from detection in db.PrivacyDetections.AsNoTracking()
                          join artifact in db.Artifacts.AsNoTracking() on detection.ArtifactId equals artifact.Id
                          where detection.ProjectId == projectId && detection.ImportSnapshotId == importId
                          orderby detection.Severity descending, artifact.RelativePath
                          select new { detection, artifact.RelativePath }).ToListAsync(cancellationToken);
        return Results.Ok(rows.Select(x => new PrivacyDetectionDto(x.detection.Id, x.detection.ArtifactId, x.RelativePath, x.detection.Kind,
            x.detection.Severity, x.detection.SourceLocation, x.detection.MaskedPreview, x.detection.Status)));
    }

    private static async Task<IResult> UpdatePrivacyDetectionAsync(Guid projectId, Guid importId, Guid detectionId, UpdatePrivacyDetectionRequest request, IDbContextFactory<WorkbenchDbContext> factory, CancellationToken cancellationToken)
    {
        var allowed = new[] { "Open", "Confirmed", "Dismissed", "Redacted" };
        var status = allowed.FirstOrDefault(x => x.Equals(request.Status?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (status is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Use Open, Confirmed, Dismissed, or Redacted."] });
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var detection = await db.PrivacyDetections.SingleOrDefaultAsync(x => x.Id == detectionId && x.ProjectId == projectId && x.ImportSnapshotId == importId, cancellationToken);
        if (detection is null) return Results.NotFound();
        detection.Status = status;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { detection.Id, detection.Status });
    }

    private static async Task<IResult> CreateRedactedExportAsync(Guid projectId, Guid importId, PrivacyService service, CancellationToken cancellationToken)
    {
        var export = await service.CreateRedactedExportAsync(projectId, importId, cancellationToken);
        return Results.File(export.Path, "application/zip", export.FileName, enableRangeProcessing: true);
    }

    private static async Task<IResult> GetPlaybooksAsync(Guid projectId, IDbContextFactory<WorkbenchDbContext> factory, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var items = await db.Playbooks.AsNoTracking().Where(x => x.ProjectId == projectId).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return Results.Ok(items.Select(PlaybookService.ToDto));
    }

    private static async Task<IResult> CreatePlaybookAsync(Guid projectId, CreatePlaybookRequest request, IDbContextFactory<WorkbenchDbContext> factory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Steps.Count == 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["playbook"] = ["A name and at least one step are required."] });
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (!await db.Projects.AnyAsync(x => x.Id == projectId, cancellationToken)) return Results.NotFound();
        var now = DateTimeOffset.UtcNow;
        var entity = new PlaybookEntity
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Name = request.Name.Trim(), Description = request.Description.Trim(),
            StepsJson = JsonSerializer.Serialize(request.Steps, JsonOptions), CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.Playbooks.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/projects/{projectId}/playbooks/{entity.Id}", PlaybookService.ToDto(entity));
    }

    private static async Task<IResult> RunPlaybookAsync(Guid projectId, Guid playbookId, Guid importId, PlaybookService service, CancellationToken cancellationToken)
    {
        try { return Results.Ok(await service.RunAsync(playbookId, importId, cancellationToken)); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    }

    private static WatchFolderDto ToDto(WatchFolderEntity entity)
    {
        IReadOnlyList<string> patterns;
        try { patterns = JsonSerializer.Deserialize<string[]>(entity.IgnorePatternsJson, JsonOptions) ?? []; }
        catch (JsonException) { patterns = []; }
        return new WatchFolderDto(entity.Id, entity.ProjectId, entity.Name, entity.FolderPath, entity.Enabled, entity.TriggerMode.ToString(),
            entity.ScanIntervalMinutes, patterns, entity.RequireApproval, entity.LastScannedAtUtc, entity.LastImportId);
    }
}
