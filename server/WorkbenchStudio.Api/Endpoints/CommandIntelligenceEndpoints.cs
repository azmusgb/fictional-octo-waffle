using WorkbenchStudio.Api.Domain;
using WorkbenchStudio.Api.Services;

namespace WorkbenchStudio.Api.Endpoints;

public static class CommandIntelligenceEndpoints
{
    public static IEndpointRouteBuilder MapCommandIntelligenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects/{projectId:guid}").WithTags("Command Intelligence");

        group.MapGet("/queue-policies", async (Guid projectId, CommandIntelligenceService service, CancellationToken ct) => Results.Ok(await service.GetQueuePoliciesAsync(projectId, ct)));
        group.MapPost("/queue-policies", async (Guid projectId, CreateQueuePolicyRequest request, CommandIntelligenceService service, CancellationToken ct) =>
        {
            try { var created = await service.CreateQueuePolicyAsync(projectId, request, ct); return Results.Created($"/api/projects/{projectId}/queue-policies/{created.Id}", created); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });
        group.MapPatch("/queue-policies/{policyId:guid}", async (Guid projectId, Guid policyId, UpdateQueuePolicyRequest request, CommandIntelligenceService service, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.UpdateQueuePolicyAsync(projectId, policyId, request, ct)); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });
        group.MapGet("/imports/{importId:guid}/adaptive-queue", async (Guid projectId, Guid importId, Guid? policyId, CommandIntelligenceService service, CancellationToken ct) => Results.Ok(await service.GetAdaptiveQueueAsync(projectId, importId, policyId, ct)));

        group.MapGet("/scenarios", async (Guid projectId, CommandIntelligenceService service, CancellationToken ct) => Results.Ok(await service.GetScenariosAsync(projectId, ct)));
        group.MapPost("/scenarios", async (Guid projectId, CreateScenarioRunRequest request, CommandIntelligenceService service, CancellationToken ct) =>
        {
            try { var created = await service.RunScenarioAsync(projectId, request, ct); return Results.Created($"/api/projects/{projectId}/scenarios/{created.Id}", created); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        group.MapGet("/approval-gates", async (Guid projectId, CommandIntelligenceService service, CancellationToken ct) => Results.Ok(await service.GetApprovalGatesAsync(projectId, ct)));
        group.MapPost("/approval-gates", async (Guid projectId, CreateApprovalGateRequest request, CommandIntelligenceService service, CancellationToken ct) =>
        {
            try { var created = await service.CreateApprovalGateAsync(projectId, request, ct); return Results.Created($"/api/projects/{projectId}/approval-gates/{created.Id}", created); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });
        group.MapPost("/approval-gates/{gateId:guid}/decision", async (Guid projectId, Guid gateId, ApprovalDecisionRequest request, CommandIntelligenceService service, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.DecideApprovalGateAsync(projectId, gateId, request, ct)); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
            catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        group.MapGet("/imports/{importId:guid}/anomaly-explanations", async (Guid projectId, Guid importId, CommandIntelligenceService service, CancellationToken ct) => Results.Ok(await service.GetAnomalyExplanationsAsync(projectId, importId, ct)));
        group.MapGet("/imports/{importId:guid}/executive-summary", async (Guid projectId, Guid importId, CommandIntelligenceService service, CancellationToken ct) => Results.Ok(await service.GetExecutiveSummaryAsync(projectId, importId, ct)));
        group.MapGet("/imports/{importId:guid}/executive-brief", async (Guid projectId, Guid importId, CommandIntelligenceService service, CancellationToken ct) =>
        {
            try { var export = await service.CreateExecutiveBriefAsync(projectId, importId, ct); return Results.File(export.StoragePath, export.ContentType, export.FileName, enableRangeProcessing: true); }
            catch (InvalidOperationException) { return Results.NotFound(); }
        });
        return app;
    }
}
