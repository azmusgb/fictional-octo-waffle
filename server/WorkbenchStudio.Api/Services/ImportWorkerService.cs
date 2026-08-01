using Microsoft.EntityFrameworkCore;
using WorkbenchStudio.Api.Data;
using WorkbenchStudio.Api.Domain;

namespace WorkbenchStudio.Api.Services;

public sealed class ImportWorkerService(
    IImportQueue queue,
    ImportProcessor processor,
    IDbContextFactory<WorkbenchDbContext> dbContextFactory,
    ILogger<ImportWorkerService> logger) : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await RecoverInterruptedImportsAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Guid importId;
            try
            {
                importId = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await processor.ProcessAsync(importId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Import worker stopped while processing {ImportId}.", importId);
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unhandled error while processing import {ImportId}.", importId);
                await MarkFailedAsync(importId, exception.Message, stoppingToken);
            }
        }
    }

    private async Task RecoverInterruptedImportsAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var recoverable = await db.Imports
            .Where(x => x.Status == ImportStatus.Queued ||
                        x.Status == ImportStatus.Preparing ||
                        x.Status == ImportStatus.Extracting ||
                        x.Status == ImportStatus.Inventorying ||
                        x.Status == ImportStatus.Parsing ||
                        x.Status == ImportStatus.Validating ||
                        x.Status == ImportStatus.Indexing)
            .ToListAsync(cancellationToken);

        foreach (var import in recoverable)
        {
            import.Status = ImportStatus.Queued;
            import.CurrentStage = "Queued";
            import.StatusMessage = "Recovered after application restart.";
            import.StartedAtUtc = null;
            import.CompletedAtUtc = null;
            import.CancellationRequested = false;
            await queue.QueueAsync(import.Id, cancellationToken);
        }

        if (recoverable.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task MarkFailedAsync(Guid importId, string message, CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var import = await db.Imports.FindAsync([importId], cancellationToken);
            if (import is null)
            {
                return;
            }

            import.Status = ImportStatus.Failed;
            import.CurrentStage = "Failed";
            import.StatusMessage = message;
            import.ErrorCount++;
            import.CompletedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not persist failed status for import {ImportId}.", importId);
        }
    }
}
