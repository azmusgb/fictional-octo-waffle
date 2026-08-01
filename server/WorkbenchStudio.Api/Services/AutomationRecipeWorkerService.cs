using Microsoft.EntityFrameworkCore;
using WorkbenchStudio.Api.Data;
using WorkbenchStudio.Api.Domain;

namespace WorkbenchStudio.Api.Services;

public sealed class AutomationRecipeWorkerService(
    IServiceScopeFactory scopeFactory,
    ILogger<AutomationRecipeWorkerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDueRecipesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Automatic decision recipe scan failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    private async Task RunDueRecipesAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>();
        var service = scope.ServiceProvider.GetRequiredService<DecisionOperationsService>();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var recipes = await db.AutomationRecipes.AsNoTracking()
            .Where(x => x.Enabled && x.Status != AutomationRecipeStatus.Running && x.TriggerMode != "Manual")
            .ToListAsync(cancellationToken);

        foreach (var recipe in recipes)
        {
            var latestImport = await db.Imports.AsNoTracking()
                .Where(x => x.ProjectId == recipe.ProjectId && (x.Status == ImportStatus.Completed || x.Status == ImportStatus.CompletedWithWarnings))
                .OrderByDescending(x => x.CompletedAtUtc ?? x.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            if (latestImport is null || !IsDue(recipe, latestImport)) continue;

            logger.LogInformation("Running automatic recipe {RecipeId} for import {ImportId}.", recipe.Id, latestImport.Id);
            await service.RunRecipeAsync(recipe.ProjectId, recipe.Id, latestImport.Id, cancellationToken);
        }
    }

    private static bool IsDue(AutomationRecipeEntity recipe, ImportSnapshotEntity latestImport)
    {
        var now = DateTimeOffset.UtcNow;
        return recipe.TriggerMode switch
        {
            "OnSnapshot" => !recipe.LastRunAtUtc.HasValue || recipe.LastRunAtUtc.Value < (latestImport.CompletedAtUtc ?? latestImport.CreatedAtUtc),
            "Hourly" => !recipe.LastRunAtUtc.HasValue || now - recipe.LastRunAtUtc.Value >= TimeSpan.FromMinutes(Math.Max(60, recipe.ScheduleIntervalMinutes)),
            "Daily" => !recipe.LastRunAtUtc.HasValue || now - recipe.LastRunAtUtc.Value >= TimeSpan.FromMinutes(Math.Max(1440, recipe.ScheduleIntervalMinutes)),
            _ => false
        };
    }
}
