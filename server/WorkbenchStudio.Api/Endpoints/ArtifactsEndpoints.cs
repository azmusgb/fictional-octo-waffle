using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkbenchStudio.Api.Data;
using WorkbenchStudio.Api.Domain;
using WorkbenchStudio.Api.Services;

namespace WorkbenchStudio.Api.Endpoints;

public static class ArtifactsEndpoints
{
    public static IEndpointRouteBuilder MapArtifactsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/artifacts").WithTags("Artifacts");
        group.MapGet("/{artifactId:guid}", GetArtifactAsync);
        group.MapGet("/{artifactId:guid}/content", GetArtifactContentAsync);
        group.MapPatch("/{artifactId:guid}/review", UpdateReviewAsync);
        return app;
    }

    private static async Task<IResult> GetArtifactAsync(
        Guid artifactId,
        IDbContextFactory<WorkbenchDbContext> factory,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var artifact = await db.Artifacts.AsNoTracking()
            .Include(x => x.Findings)
            .Include(x => x.Review)
            .SingleOrDefaultAsync(x => x.Id == artifactId, cancellationToken);
        if (artifact is null) return Results.NotFound();

        var findingDtos = artifact.Findings
            .OrderByDescending(x => x.Severity)
            .ThenBy(x => x.RuleId)
            .Select(x => DtoMapper.ToDto(x, artifact.RelativePath))
            .ToArray();
        return Results.Ok(new ArtifactDetailDto(
            DtoMapper.ToDto(artifact, findingDtos.Length),
            DtoMapper.ParseJsonElement(artifact.StructureSummaryJson),
            artifact.PreviewText,
            artifact.ParseError,
            findingDtos,
            DtoMapper.ToDto(artifact.Review)));
    }

    private static async Task<IResult> UpdateReviewAsync(
        Guid artifactId,
        UpdateArtifactReviewRequest request,
        IDbContextFactory<WorkbenchDbContext> factory,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ArtifactReviewStatus>(request.Status, true, out var status))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = ["Use Unreviewed, InReview, Accepted, or NeedsAttention."]
            });
        }

        var note = request.Note?.Trim();
        if (note?.Length > 4000)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["note"] = ["Review notes cannot exceed 4,000 characters."]
            });
        }

        var tags = (request.Tags ?? [])
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .Select(value => value.Length <= 40 ? value : value[..40])
            .ToArray();

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var artifact = await db.Artifacts
            .Include(x => x.ImportSnapshot)
            .SingleOrDefaultAsync(x => x.Id == artifactId, cancellationToken);
        if (artifact is null) return Results.NotFound();

        var review = await db.ArtifactReviews.SingleOrDefaultAsync(x => x.ArtifactId == artifactId, cancellationToken);
        if (review is null)
        {
            review = new ArtifactReviewEntity { ArtifactId = artifactId };
            db.ArtifactReviews.Add(review);
        }

        review.Status = status;
        review.Note = string.IsNullOrWhiteSpace(note) ? null : note;
        review.TagsJson = tags.Length == 0 ? null : JsonSerializer.Serialize(tags);
        review.UpdatedAtUtc = DateTimeOffset.UtcNow;
        if (artifact.ImportSnapshot is not null)
        {
            var project = await db.Projects.SingleOrDefaultAsync(
                x => x.Id == artifact.ImportSnapshot.ProjectId,
                cancellationToken);
            if (project is not null)
            {
                project.UpdatedAtUtc = review.UpdatedAtUtc;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(DtoMapper.ToDto(review));
    }

    private static async Task<IResult> GetArtifactContentAsync(
        Guid artifactId,
        IDbContextFactory<WorkbenchDbContext> factory,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var artifact = await db.Artifacts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == artifactId, cancellationToken);
        if (artifact is null || !File.Exists(artifact.StoragePath)) return Results.NotFound();
        return Results.File(artifact.StoragePath, artifact.MediaType, artifact.Name, enableRangeProcessing: true);
    }
}
