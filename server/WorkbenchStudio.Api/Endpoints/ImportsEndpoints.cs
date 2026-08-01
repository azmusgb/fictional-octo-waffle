using Microsoft.EntityFrameworkCore;
using WorkbenchStudio.Api.Data;
using WorkbenchStudio.Api.Domain;
using WorkbenchStudio.Api.Services;

namespace WorkbenchStudio.Api.Endpoints;

public static class ImportsEndpoints
{
    public static IEndpointRouteBuilder MapImportsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/imports").WithTags("Imports");
        group.MapGet("/{importId:guid}", GetImportAsync);
        group.MapPost("/{importId:guid}/cancel", CancelImportAsync);
        group.MapPost("/{importId:guid}/retry", RetryImportAsync);
        group.MapGet("/{importId:guid}/artifacts", GetArtifactsAsync);
        return app;
    }

    private static async Task<IResult> GetImportAsync(
        Guid importId,
        IDbContextFactory<WorkbenchDbContext> factory,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var import = await db.Imports.AsNoTracking().SingleOrDefaultAsync(x => x.Id == importId, cancellationToken);
        return import is null ? Results.NotFound() : Results.Ok(DtoMapper.ToDto(import));
    }

    private static async Task<IResult> CancelImportAsync(
        Guid importId,
        IDbContextFactory<WorkbenchDbContext> factory,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var import = await db.Imports.SingleOrDefaultAsync(x => x.Id == importId, cancellationToken);
        if (import is null)
        {
            return Results.NotFound();
        }

        if (import.Status is ImportStatus.Completed or ImportStatus.CompletedWithWarnings or ImportStatus.Failed or ImportStatus.Cancelled)
        {
            return Results.Conflict(new { error = "The import is no longer running." });
        }

        import.CancellationRequested = true;
        import.StatusMessage = "Cancellation requested.";
        await db.SaveChangesAsync(cancellationToken);
        return Results.Accepted($"/api/imports/{import.Id}", DtoMapper.ToDto(import));
    }

    private static async Task<IResult> RetryImportAsync(
        Guid importId,
        IDbContextFactory<WorkbenchDbContext> factory,
        IImportQueue queue,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var import = await db.Imports.SingleOrDefaultAsync(x => x.Id == importId, cancellationToken);
        if (import is null)
        {
            return Results.NotFound();
        }

        if (import.Status is not (ImportStatus.Failed or ImportStatus.Cancelled))
        {
            return Results.Conflict(new { error = "Only failed or cancelled imports can be retried." });
        }

        if (string.IsNullOrWhiteSpace(import.StagingPath) ||
            !Directory.Exists(import.StagingPath) ||
            !Directory.EnumerateFiles(import.StagingPath, "*", SearchOption.TopDirectoryOnly).Any())
        {
            return Results.Conflict(new { error = "The original staged files are no longer available for retry." });
        }

        import.Status = ImportStatus.Queued;
        import.CurrentStage = "Queued";
        import.StatusMessage = "Retry queued using the original staged files.";
        import.StartedAtUtc = null;
        import.CompletedAtUtc = null;
        import.ProcessedFiles = 0;
        import.WarningCount = 0;
        import.ErrorCount = 0;
        import.CancellationRequested = false;
        await db.SaveChangesAsync(cancellationToken);
        await queue.QueueAsync(import.Id, cancellationToken);
        return Results.Accepted($"/api/imports/{import.Id}", DtoMapper.ToDto(import));
    }

    private static async Task<IResult> GetArtifactsAsync(
        Guid importId,
        string? search,
        string? extension,
        string? status,
        int? offset,
        int? limit,
        IDbContextFactory<WorkbenchDbContext> factory,
        CancellationToken cancellationToken)
    {
        var safeOffset = Math.Max(0, offset ?? 0);
        var safeLimit = Math.Clamp(limit ?? 500, 1, 2_000);

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var query = db.Artifacts.AsNoTracking().Include(x => x.Review).Where(x => x.ImportSnapshotId == importId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => EF.Functions.Like(x.RelativePath, $"%{term}%") ||
                                     EF.Functions.Like(x.Name, $"%{term}%") ||
                                     EF.Functions.Like(x.Sha256, $"%{term}%") ||
                                     EF.Functions.Like(x.MediaType, $"%{term}%") ||
                                     (x.ParserId != null && EF.Functions.Like(x.ParserId, $"%{term}%")));
        }

        if (!string.IsNullOrWhiteSpace(extension))
        {
            var normalized = extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
            query = query.Where(x => x.Extension == normalized);
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ArtifactParseStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(x => x.ParseStatus == parsedStatus);
        }

        var total = await query.CountAsync(cancellationToken);
        var artifacts = await query
            .OrderBy(x => x.RelativePath)
            .Skip(safeOffset)
            .Take(safeLimit)
            .ToListAsync(cancellationToken);
        var artifactIds = artifacts.Select(x => x.Id).ToArray();
        var counts = await db.Findings.AsNoTracking()
            .Where(x => x.ArtifactId.HasValue && artifactIds.Contains(x.ArtifactId.Value))
            .GroupBy(x => x.ArtifactId!.Value)
            .ToDictionaryAsync(group => group.Key, group => group.Count(), cancellationToken);

        return Results.Ok(new
        {
            total,
            offset = safeOffset,
            limit = safeLimit,
            items = artifacts.Select(x => DtoMapper.ToDto(x, counts.GetValueOrDefault(x.Id)))
        });
    }
}
