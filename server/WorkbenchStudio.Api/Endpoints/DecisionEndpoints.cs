using WorkbenchStudio.Api.Domain;
using WorkbenchStudio.Api.Services;

namespace WorkbenchStudio.Api.Endpoints;

public static class DecisionEndpoints
{
    public static IEndpointRouteBuilder MapDecisionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects/{projectId:guid}").WithTags("Decision Operations");

        group.MapGet("/imports/{importId:guid}/triage", async (Guid projectId, Guid importId, DecisionOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.GetTriageAsync(projectId, importId, ct)));

        group.MapGet("/baselines", async (Guid projectId, DecisionOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.GetBaselinesAsync(projectId, ct)));
        group.MapPost("/baselines", async (Guid projectId, CreateBaselinePolicyRequest request, DecisionOperationsService service, CancellationToken ct) =>
        {
            try { var created = await service.CreateBaselineAsync(projectId, request, ct); return Results.Created($"/api/projects/{projectId}/baselines/{created.Id}", created); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });
        group.MapPost("/baselines/{policyId:guid}/evaluate/{importId:guid}", async (Guid projectId, Guid policyId, Guid importId, DecisionOperationsService service, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.EvaluateBaselineAsync(projectId, policyId, importId, ct)); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        group.MapGet("/automation-recipes", async (Guid projectId, DecisionOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.GetRecipesAsync(projectId, ct)));
        group.MapPost("/automation-recipes", async (Guid projectId, CreateAutomationRecipeRequest request, DecisionOperationsService service, CancellationToken ct) =>
        {
            try { var created = await service.CreateRecipeAsync(projectId, request, ct); return Results.Created($"/api/projects/{projectId}/automation-recipes/{created.Id}", created); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });
        group.MapPatch("/automation-recipes/{recipeId:guid}", async (Guid projectId, Guid recipeId, UpdateAutomationRecipeRequest request, DecisionOperationsService service, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.UpdateRecipeAsync(projectId, recipeId, request, ct)); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });
        group.MapPost("/automation-recipes/{recipeId:guid}/run/{importId:guid}", async (Guid projectId, Guid recipeId, Guid importId, DecisionOperationsService service, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.RunRecipeAsync(projectId, recipeId, importId, ct)); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        group.MapPost("/imports/{importId:guid}/evidence-assistant/ask", async (Guid projectId, Guid importId, EvidenceQuestionRequest request, DecisionOperationsService service, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.AskEvidenceAsync(projectId, importId, request, ct)); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        });

        group.MapGet("/imports/{importId:guid}/decision-brief", async (Guid projectId, Guid importId, DecisionOperationsService service, CancellationToken ct) =>
        {
            try
            {
                var export = await service.CreateDecisionBriefAsync(projectId, importId, ct);
                return Results.File(export.StoragePath, export.ContentType, export.FileName, enableRangeProcessing: true);
            }
            catch (InvalidOperationException) { return Results.NotFound(); }
        });

        return app;
    }
}
