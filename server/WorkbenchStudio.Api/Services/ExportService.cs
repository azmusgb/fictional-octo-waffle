using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkbenchStudio.Api.Data;
using WorkbenchStudio.Api.Domain;

namespace WorkbenchStudio.Api.Services;

public sealed record GeneratedExport(string FileName, string ContentType, string StoragePath);

public sealed class ExportService(
    IDbContextFactory<WorkbenchDbContext> dbContextFactory,
    FileStorageService storage)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<GeneratedExport?> GenerateAsync(
        Guid projectId,
        Guid importId,
        string format,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(x => x.Id == projectId, cancellationToken);
        var import = await db.Imports.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == importId && x.ProjectId == projectId,
            cancellationToken);
        if (project is null || import is null)
        {
            return null;
        }

        var artifacts = await db.Artifacts.AsNoTracking()
            .Where(x => x.ImportSnapshotId == importId)
            .OrderBy(x => x.RelativePath)
            .ToListAsync(cancellationToken);
        var findings = await db.Findings.AsNoTracking()
            .Include(x => x.Artifact)
            .Where(x => x.ImportSnapshotId == importId)
            .OrderByDescending(x => x.Severity)
            .ThenBy(x => x.Artifact!.RelativePath)
            .ToListAsync(cancellationToken);

        var normalizedFormat = format.Trim().ToLowerInvariant();
        var safeProjectName = MakeSafeFileName(project.Name);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var paths = storage.EnsureImportPaths(projectId, importId);

        var generated = normalizedFormat switch
        {
            "json" => await GenerateJsonAsync(safeProjectName, timestamp, project, import, artifacts, findings, paths.Exports, cancellationToken),
            "csv" => await GenerateCsvAsync(safeProjectName, timestamp, import, artifacts, paths.Exports, cancellationToken),
            "html" => await GenerateHtmlAsync(safeProjectName, timestamp, project, import, artifacts, findings, paths.Exports, cancellationToken),
            _ => null
        };

        if (generated is null)
        {
            return null;
        }

        var info = new FileInfo(generated.StoragePath);
        db.Exports.Add(new ExportRecordEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ImportSnapshotId = importId,
            Format = normalizedFormat,
            FileName = generated.FileName,
            StoragePath = generated.StoragePath,
            SizeBytes = info.Length,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return generated;
    }

    private static async Task<GeneratedExport> GenerateJsonAsync(
        string projectName,
        string timestamp,
        ProjectEntity project,
        ImportSnapshotEntity import,
        IReadOnlyList<ArtifactEntity> artifacts,
        IReadOnlyList<FindingEntity> findings,
        string exportDirectory,
        CancellationToken cancellationToken)
    {
        var fileName = $"{projectName}-{timestamp}-inventory.json";
        var path = Path.Combine(exportDirectory, fileName);
        var payload = new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            project = new { project.Id, project.Name, project.CreatedAtUtc, project.UpdatedAtUtc },
            import = DtoMapper.ToDto(import),
            artifacts = artifacts.Select(x => DtoMapper.ToDto(x, findings.Count(f => f.ArtifactId == x.Id))),
            findings = findings.Select(x => DtoMapper.ToDto(x))
        };

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, payload, JsonOptions, cancellationToken);
        return new GeneratedExport(fileName, "application/json", path);
    }

    private static async Task<GeneratedExport> GenerateCsvAsync(
        string projectName,
        string timestamp,
        ImportSnapshotEntity import,
        IReadOnlyList<ArtifactEntity> artifacts,
        string exportDirectory,
        CancellationToken cancellationToken)
    {
        var fileName = $"{projectName}-{timestamp}-inventory.csv";
        var path = Path.Combine(exportDirectory, fileName);
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await writer.WriteLineAsync("ImportId,RelativePath,Name,Extension,MediaType,SizeBytes,Sha256,ParseStatus,ParserId");

        foreach (var artifact in artifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = string.Join(',',
                EscapeCsv(import.Id.ToString()),
                EscapeCsv(artifact.RelativePath),
                EscapeCsv(artifact.Name),
                EscapeCsv(artifact.Extension),
                EscapeCsv(artifact.MediaType),
                artifact.SizeBytes.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(artifact.Sha256),
                EscapeCsv(artifact.ParseStatus.ToString()),
                EscapeCsv(artifact.ParserId ?? string.Empty));
            await writer.WriteLineAsync(row);
        }

        await writer.FlushAsync(cancellationToken);
        return new GeneratedExport(fileName, "text/csv; charset=utf-8", path);
    }

    private static async Task<GeneratedExport> GenerateHtmlAsync(
        string projectName,
        string timestamp,
        ProjectEntity project,
        ImportSnapshotEntity import,
        IReadOnlyList<ArtifactEntity> artifacts,
        IReadOnlyList<FindingEntity> findings,
        string exportDirectory,
        CancellationToken cancellationToken)
    {
        var fileName = $"{projectName}-{timestamp}-report.html";
        var path = Path.Combine(exportDirectory, fileName);
        var totalBytes = artifacts.Sum(x => x.SizeBytes);
        var parsedCount = artifacts.Count(x => x.ParseStatus is ArtifactParseStatus.Parsed or ArtifactParseStatus.ParsedWithWarnings);
        var unsupportedCount = artifacts.Count(x => x.ParseStatus == ArtifactParseStatus.Unsupported);

        var builder = new StringBuilder(64 * 1024);
        builder.Append(
            """
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <title>Workbench Studio Report</title>
            <style>
            :root{font-family:Inter,ui-sans-serif,system-ui,-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif;color:#172033;background:#f4f6f9}body{margin:0;padding:40px}.report{max-width:1200px;margin:auto}.hero{background:#fff;border:1px solid #dfe4ec;border-radius:18px;padding:28px;box-shadow:0 12px 32px rgba(25,35,55,.07)}h1{margin:0 0 8px;font-size:30px}.muted{color:#667085}.metrics{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px;margin:22px 0}.metric{background:#f8fafc;border:1px solid #e4e9f0;border-radius:12px;padding:16px}.metric strong{display:block;font-size:24px}.section{margin-top:24px;background:#fff;border:1px solid #dfe4ec;border-radius:18px;padding:22px;overflow:hidden}table{width:100%;border-collapse:collapse;font-size:13px}th,td{text-align:left;padding:10px;border-bottom:1px solid #edf0f4;vertical-align:top}th{background:#f8fafc;position:sticky;top:0}.sev-Error{color:#b42318;font-weight:700}.sev-Warning{color:#b54708;font-weight:700}.sev-Info{color:#175cd3;font-weight:700}code{font-family:"Cascadia Code",Consolas,monospace;font-size:12px}@media(max-width:760px){body{padding:16px}.metrics{grid-template-columns:repeat(2,1fr)}.section{overflow:auto}}
            </style>
            </head>
            <body><main class="report">
            """);
        builder.Append("<section class=\"hero\"><h1>")
            .Append(Html(project.Name))
            .Append("</h1><p class=\"muted\">Workbench Studio inventory and validation report</p>")
            .Append("<p><strong>Snapshot:</strong> ").Append(Html(import.DisplayName)).Append("<br>")
            .Append("<strong>Generated:</strong> ").Append(Html(DateTimeOffset.UtcNow.ToString("u", CultureInfo.InvariantCulture))).Append("</p>")
            .Append("<div class=\"metrics\">")
            .Append(Metric("Artifacts", artifacts.Count.ToString("N0", CultureInfo.InvariantCulture)))
            .Append(Metric("Total size", FormatBytes(totalBytes)))
            .Append(Metric("Parsed", parsedCount.ToString("N0", CultureInfo.InvariantCulture)))
            .Append(Metric("Findings", findings.Count.ToString("N0", CultureInfo.InvariantCulture)))
            .Append("</div><p class=\"muted\">")
            .Append(unsupportedCount.ToString("N0", CultureInfo.InvariantCulture))
            .Append(" artifacts use unsupported formats and remain available in the inventory.</p></section>");

        builder.Append("<section class=\"section\"><h2>Findings</h2><table><thead><tr><th>Severity</th><th>Rule</th><th>Artifact</th><th>Finding</th><th>Evidence</th></tr></thead><tbody>");
        foreach (var finding in findings)
        {
            builder.Append("<tr><td class=\"sev-").Append(Html(finding.Severity.ToString())).Append("\">")
                .Append(Html(finding.Severity.ToString())).Append("</td><td><code>")
                .Append(Html(finding.RuleId)).Append("</code></td><td>")
                .Append(Html(finding.Artifact?.RelativePath ?? "Project")).Append("</td><td><strong>")
                .Append(Html(finding.Title)).Append("</strong><br>").Append(Html(finding.Message)).Append("</td><td>")
                .Append(Html(finding.SourceLocation ?? string.Empty)).Append("<br>")
                .Append(Html(finding.EvidenceExcerpt ?? string.Empty)).Append("</td></tr>");
        }
        builder.Append("</tbody></table></section>");

        builder.Append("<section class=\"section\"><h2>Artifact inventory</h2><table><thead><tr><th>Path</th><th>Type</th><th>Size</th><th>Status</th><th>SHA-256</th></tr></thead><tbody>");
        foreach (var artifact in artifacts)
        {
            builder.Append("<tr><td>").Append(Html(artifact.RelativePath)).Append("</td><td>")
                .Append(Html(artifact.MediaType)).Append("</td><td>")
                .Append(Html(FormatBytes(artifact.SizeBytes))).Append("</td><td>")
                .Append(Html(artifact.ParseStatus.ToString())).Append("</td><td><code>")
                .Append(Html(artifact.Sha256)).Append("</code></td></tr>");
        }
        builder.Append("</tbody></table></section></main></body></html>");

        await File.WriteAllTextAsync(path, builder.ToString(), new UTF8Encoding(false), cancellationToken);
        return new GeneratedExport(fileName, "text/html; charset=utf-8", path);
    }

    private static string EscapeCsv(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string Html(string value) => WebUtility.HtmlEncode(value);
    private static string Metric(string label, string value) =>
        $"<div class=\"metric\"><span class=\"muted\">{Html(label)}</span><strong>{Html(value)}</strong></div>";

    private static string MakeSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var safe = new string(value.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "workbench-project" : safe.Replace(' ', '-');
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }
}
