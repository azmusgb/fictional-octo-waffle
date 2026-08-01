using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WorkbenchStudio.Api.Data;
using WorkbenchStudio.Api.Domain;
using WorkbenchStudio.Api.Parsing;
using WorkbenchStudio.Api.Services;

namespace WorkbenchStudio.Api.Endpoints;

public static class SystemEndpoints
{
    private static readonly DateTimeOffset StartedAtUtc = DateTimeOffset.UtcNow;

    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/system").WithTags("System");
        group.MapGet("/status", GetStatusAsync);
        return app;
    }

    private static async Task<IResult> GetStatusAsync(
        FileStorageService storage,
        ParserRegistry registry,
        IOptions<WorkspaceOptions> options,
        IDbContextFactory<WorkbenchDbContext> factory,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var projectCount = await db.Projects.AsNoTracking().CountAsync(cancellationToken);
        var importCount = await db.Imports.AsNoTracking().CountAsync(cancellationToken);
        var artifactCount = await db.Artifacts.AsNoTracking().CountAsync(cancellationToken);
        var findingCount = await db.Findings.AsNoTracking().CountAsync(cancellationToken);
        var queuedImportCount = await db.Imports.AsNoTracking().CountAsync(
            item => item.Status == ImportStatus.Queued ||
                    item.Status == ImportStatus.Preparing ||
                    item.Status == ImportStatus.Extracting ||
                    item.Status == ImportStatus.Inventorying ||
                    item.Status == ImportStatus.Parsing ||
                    item.Status == ImportStatus.Validating ||
                    item.Status == ImportStatus.Indexing,
            cancellationToken);

        var root = Path.GetPathRoot(storage.RootPath) ?? storage.RootPath;
        var drive = new DriveInfo(root);
        var databaseSize = File.Exists(storage.DatabasePath) ? new FileInfo(storage.DatabasePath).Length : 0L;
        var limits = options.Value;
        return Results.Ok(new AgentStatusDto(
            "Healthy",
            "Workbench Studio Local Agent",
            "6.0.0",
            DateTimeOffset.UtcNow,
            Math.Max(0, (long)(DateTimeOffset.UtcNow - StartedAtUtc).TotalSeconds),
            drive.AvailableFreeSpace,
            drive.TotalSize,
            databaseSize,
            projectCount,
            importCount,
            artifactCount,
            findingCount,
            queuedImportCount,
            registry.ParserIds,
            new WorkspaceLimitsDto(
                limits.MaximumUploadBytes,
                limits.MaximumSingleFileBytes,
                limits.MaximumExtractedBytes,
                limits.MaximumExtractedFiles,
                limits.MaximumCompressionRatio)));
    }
}
