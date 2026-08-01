using Microsoft.EntityFrameworkCore;
using WorkbenchStudio.Api.Data;
using WorkbenchStudio.Api.Domain;
using WorkbenchStudio.Api.Services;

namespace WorkbenchStudio.Api.Endpoints;

public static class ProjectsEndpoints
{
    public static IEndpointRouteBuilder MapProjectsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects").WithTags("Projects");

        group.MapGet("/", GetProjectsAsync);
        group.MapPost("/", CreateProjectAsync);
        group.MapGet("/{projectId:guid}", GetProjectAsync);
        group.MapPatch("/{projectId:guid}", UpdateProjectAsync);
        group.MapGet("/{projectId:guid}/imports", GetImportsAsync);
        group.MapPost("/{projectId:guid}/imports", CreateImportAsync)
            .DisableAntiforgery();
        group.MapGet("/{projectId:guid}/findings", GetFindingsAsync);
        group.MapGet("/{projectId:guid}/search", SearchAsync);
        group.MapPost("/{projectId:guid}/compare", CompareAsync);
        group.MapGet("/{projectId:guid}/imports/{importId:guid}/export/{format}", ExportAsync);
        group.MapGet("/{projectId:guid}/manifest", ManifestAsync);

        return app;
    }

    private static async Task<IResult> GetProjectsAsync(
        IDbContextFactory<WorkbenchDbContext> factory,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var projects = await db.Projects.AsNoTracking()
            .Include(x => x.Imports)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        var response = projects.Select(project =>
        {
            var latest = project.Imports.OrderByDescending(x => x.CreatedAtUtc).FirstOrDefault();
            return DtoMapper.ToDto(project, latest);
        });
        return Results.Ok(response);
    }

    private static async Task<IResult> CreateProjectAsync(
        CreateProjectRequest request,
        IDbContextFactory<WorkbenchDbContext> factory,
        FileStorageService storage,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Project name is required."]
            });
        }

        if (name.Length > 200)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Project name cannot exceed 200 characters."]
            });
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var project = new ProjectEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);
        Directory.CreateDirectory(storage.GetProjectRoot(project.Id));
        return Results.Created($"/api/projects/{project.Id}", DtoMapper.ToDto(project));
    }

    private static async Task<IResult> UpdateProjectAsync(
        Guid projectId,
        UpdateProjectRequest request,
        IDbContextFactory<WorkbenchDbContext> factory,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Project name is required."]
            });
        }

        if (name.Length > 200)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Project name cannot exceed 200 characters."]
            });
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var project = await db.Projects
            .Include(x => x.Imports)
            .SingleOrDefaultAsync(x => x.Id == projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        project.Name = name;
        project.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        var latest = project.Imports.OrderByDescending(x => x.CreatedAtUtc).FirstOrDefault();
        return Results.Ok(DtoMapper.ToDto(project, latest));
    }

    private static async Task<IResult> GetProjectAsync(
        Guid projectId,
        IDbContextFactory<WorkbenchDbContext> factory,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var project = await db.Projects.AsNoTracking()
            .Include(x => x.Imports)
            .SingleOrDefaultAsync(x => x.Id == projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        var latest = project.Imports.OrderByDescending(x => x.CreatedAtUtc).FirstOrDefault();
        return Results.Ok(DtoMapper.ToDto(project, latest));
    }

    private static async Task<IResult> GetImportsAsync(
        Guid projectId,
        IDbContextFactory<WorkbenchDbContext> factory,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var exists = await db.Projects.AnyAsync(x => x.Id == projectId, cancellationToken);
        if (!exists)
        {
            return Results.NotFound();
        }

        var imports = await db.Imports.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return Results.Ok(imports.Select(DtoMapper.ToDto));
    }

    private static async Task<IResult> CreateImportAsync(
        Guid projectId,
        HttpRequest request,
        IDbContextFactory<WorkbenchDbContext> factory,
        FileStorageService storage,
        IImportQueue queue,
        Microsoft.Extensions.Options.IOptions<WorkspaceOptions> options,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { error = "Use multipart/form-data and provide one or more files." });
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var project = await db.Projects.SingleOrDefaultAsync(x => x.Id == projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var files = form.Files.Where(x => x.Length >= 0).ToArray();
        if (files.Length == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["files"] = ["Select at least one file."]
            });
        }

        var limits = options.Value;
        var totalBytes = files.Sum(x => x.Length);
        if (totalBytes > limits.MaximumUploadBytes)
        {
            return Results.BadRequest(new { error = $"The upload exceeds the {limits.MaximumUploadBytes:N0}-byte limit." });
        }

        var oversized = files.FirstOrDefault(x => x.Length > limits.MaximumSingleFileBytes);
        if (oversized is not null)
        {
            return Results.BadRequest(new { error = $"{oversized.FileName} exceeds the per-file size limit." });
        }

        var importId = Guid.NewGuid();
        var paths = storage.EnsureImportPaths(projectId, importId);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var safeName = MakeUniqueFileName(FileStorageService.SanitizeFileName(file.FileName), usedNames);
            var destination = storage.GetSafeDestination(paths.Staging, safeName);
            await using var output = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await file.CopyToAsync(output, cancellationToken);
        }

        var suppliedName = form["displayName"].ToString().Trim();
        var displayName = string.IsNullOrWhiteSpace(suppliedName)
            ? $"Import {DateTimeOffset.Now:yyyy-MM-dd HH:mm}"
            : suppliedName.Length <= 240 ? suppliedName : suppliedName[..240];
        var import = new ImportSnapshotEntity
        {
            Id = importId,
            ProjectId = projectId,
            DisplayName = displayName,
            Status = ImportStatus.Queued,
            CurrentStage = "Queued",
            StatusMessage = $"{files.Length:N0} uploaded file(s) queued for processing.",
            StagingPath = paths.Staging,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            TotalFiles = files.Length,
            TotalBytes = totalBytes
        };
        db.Imports.Add(import);
        project.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await queue.QueueAsync(import.Id, cancellationToken);
        return Results.Accepted($"/api/imports/{import.Id}", DtoMapper.ToDto(import));
    }

    private static async Task<IResult> GetFindingsAsync(
        Guid projectId,
        Guid? importId,
        string? severity,
        IDbContextFactory<WorkbenchDbContext> factory,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var query = db.Findings.AsNoTracking()
            .Include(x => x.Artifact)
            .Where(x => x.ProjectId == projectId);

        if (importId.HasValue)
        {
            query = query.Where(x => x.ImportSnapshotId == importId.Value);
        }

        if (!string.IsNullOrWhiteSpace(severity) &&
            Enum.TryParse<FindingSeverity>(severity, true, out var parsedSeverity))
        {
            query = query.Where(x => x.Severity == parsedSeverity);
        }

        var findings = await query
            .OrderByDescending(x => x.Severity)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(5_000)
            .ToListAsync(cancellationToken);
        return Results.Ok(findings.Select(x => DtoMapper.ToDto(x)));
    }

    private static async Task<IResult> SearchAsync(
        Guid projectId,
        string? q,
        Guid? importId,
        IDbContextFactory<WorkbenchDbContext> factory,
        CancellationToken cancellationToken)
    {
        var term = q?.Trim();
        if (string.IsNullOrWhiteSpace(term))
        {
            return Results.Ok(new SearchResultDto([], []));
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var artifactQuery = db.Artifacts.AsNoTracking()
            .Include(x => x.Review)
            .Where(x => x.ImportSnapshot!.ProjectId == projectId &&
                        (EF.Functions.Like(x.RelativePath, $"%{term}%") ||
                         EF.Functions.Like(x.Name, $"%{term}%") ||
                         EF.Functions.Like(x.Sha256, $"%{term}%") ||
                         EF.Functions.Like(x.MediaType, $"%{term}%") ||
                         (x.ParserId != null && EF.Functions.Like(x.ParserId, $"%{term}%")) ||
                         (x.Review != null && x.Review.Note != null && EF.Functions.Like(x.Review.Note, $"%{term}%")) ||
                         (x.Review != null && x.Review.TagsJson != null && EF.Functions.Like(x.Review.TagsJson, $"%{term}%"))));
        var findingQuery = db.Findings.AsNoTracking()
            .Include(x => x.Artifact)
            .Where(x => x.ProjectId == projectId &&
                        (EF.Functions.Like(x.Title, $"%{term}%") ||
                         EF.Functions.Like(x.Message, $"%{term}%") ||
                         EF.Functions.Like(x.RuleId, $"%{term}%") ||
                         (x.SourceLocation != null && EF.Functions.Like(x.SourceLocation, $"%{term}%")) ||
                         (x.EvidenceExcerpt != null && EF.Functions.Like(x.EvidenceExcerpt, $"%{term}%")) ||
                         (x.Artifact != null && EF.Functions.Like(x.Artifact.RelativePath, $"%{term}%"))));

        if (importId.HasValue)
        {
            artifactQuery = artifactQuery.Where(x => x.ImportSnapshotId == importId.Value);
            findingQuery = findingQuery.Where(x => x.ImportSnapshotId == importId.Value);
        }

        var artifacts = await artifactQuery.OrderBy(x => x.RelativePath).Take(100).ToListAsync(cancellationToken);
        var artifactIds = artifacts.Select(x => x.Id).ToArray();
        var findingCounts = await db.Findings.AsNoTracking()
            .Where(x => x.ArtifactId.HasValue && artifactIds.Contains(x.ArtifactId.Value))
            .GroupBy(x => x.ArtifactId!.Value)
            .ToDictionaryAsync(group => group.Key, group => group.Count(), cancellationToken);
        var findings = await findingQuery.OrderByDescending(x => x.Severity).Take(100).ToListAsync(cancellationToken);

        return Results.Ok(new SearchResultDto(
            artifacts.Select(x => DtoMapper.ToDto(x, findingCounts.GetValueOrDefault(x.Id))).ToArray(),
            findings.Select(x => DtoMapper.ToDto(x)).ToArray()));
    }

    private static async Task<IResult> CompareAsync(
        Guid projectId,
        CompareImportsRequest request,
        ComparisonService service,
        CancellationToken cancellationToken)
    {
        if (request.LeftImportId == request.RightImportId)
        {
            return Results.BadRequest(new { error = "Choose two different imports." });
        }

        var result = await service.CompareAsync(
            projectId,
            request.LeftImportId,
            request.RightImportId,
            cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> ExportAsync(
        Guid projectId,
        Guid importId,
        string format,
        ExportService service,
        CancellationToken cancellationToken)
    {
        if (format is not ("json" or "csv" or "html"))
        {
            return Results.BadRequest(new { error = "Supported export formats are json, csv, and html." });
        }

        var export = await service.GenerateAsync(projectId, importId, format, cancellationToken);
        return export is null
            ? Results.NotFound()
            : Results.File(export.StoragePath, export.ContentType, export.FileName, enableRangeProcessing: true);
    }

    private static string MakeUniqueFileName(string requested, HashSet<string> used)
    {
        if (used.Add(requested))
        {
            return requested;
        }

        var name = Path.GetFileNameWithoutExtension(requested);
        var extension = Path.GetExtension(requested);
        for (var index = 2; ; index++)
        {
            var candidate = $"{name} ({index}){extension}";
            if (used.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static async Task<IResult> ManifestAsync(
        Guid projectId,
        ProjectManifestService service,
        CancellationToken cancellationToken)
    {
        var manifest = await service.GenerateAsync(projectId, cancellationToken);
        return manifest is null
            ? Results.NotFound()
            : Results.File(manifest.Value.Content, "application/json", manifest.Value.FileName);
    }

}
