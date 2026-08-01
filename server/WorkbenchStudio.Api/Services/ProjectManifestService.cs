using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkbenchStudio.Api.Data;

namespace WorkbenchStudio.Api.Services;

public sealed class ProjectManifestService(IDbContextFactory<WorkbenchDbContext> factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<(byte[] Content, string FileName)?> GenerateAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var project = await db.Projects.AsNoTracking()
            .Include(x => x.Imports)
                .ThenInclude(x => x.Artifacts)
                    .ThenInclude(x => x.Findings)
            .Include(x => x.Imports)
                .ThenInclude(x => x.Artifacts)
                    .ThenInclude(x => x.Review)
            .SingleOrDefaultAsync(x => x.Id == projectId, cancellationToken);
        if (project is null) return null;

        var manifest = new
        {
            schemaVersion = "workbench-project-manifest/1.0",
            generatedAtUtc = DateTimeOffset.UtcNow,
            applicationVersion = "0.8.4",
            project = new { project.Id, project.Name, project.CreatedAtUtc, project.UpdatedAtUtc },
            imports = project.Imports.OrderBy(x => x.CreatedAtUtc).Select(import => new
            {
                import.Id,
                import.DisplayName,
                status = import.Status.ToString(),
                import.CreatedAtUtc,
                import.CompletedAtUtc,
                import.TotalFiles,
                import.TotalBytes,
                artifacts = import.Artifacts.OrderBy(x => x.RelativePath).Select(artifact => new
                {
                    artifact.Id,
                    artifact.RelativePath,
                    artifact.MediaType,
                    artifact.SizeBytes,
                    artifact.Sha256,
                    parseStatus = artifact.ParseStatus.ToString(),
                    artifact.ParserId,
                    artifact.ParserVersion,
                    review = DtoMapper.ToDto(artifact.Review),
                    findings = artifact.Findings.Select(finding => DtoMapper.ToDto(finding)).ToArray()
                }).ToArray()
            }).ToArray()
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
        var safeName = string.Join('-', project.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "workbench-project";
        return (bytes, $"{safeName}-manifest.json");
    }
}