using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using WorkbenchStudio.Api.Data;
using WorkbenchStudio.Api.Domain;

namespace WorkbenchStudio.Api.Services;

public sealed class DataProfileService(IDbContextFactory<WorkbenchDbContext> factory)
{
    public async Task<int> ProfileImportAsync(Guid projectId, Guid importId, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var artifacts = await db.Artifacts.AsNoTracking()
            .Where(x => x.ImportSnapshotId == importId)
            .OrderBy(x => x.RelativePath)
            .ToListAsync(cancellationToken);

        var artifactIds = artifacts.Select(x => x.Id).ToArray();
        var old = await db.DataProfiles.Where(x => artifactIds.Contains(x.ArtifactId)).ToListAsync(cancellationToken);
        db.DataProfiles.RemoveRange(old);

        var duplicateCounts = artifacts.GroupBy(x => x.Sha256).ToDictionary(x => x.Key, x => x.Count());
        foreach (var artifact in artifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var profile = BuildProfile(projectId, artifact, duplicateCounts.GetValueOrDefault(artifact.Sha256));
            db.DataProfiles.Add(profile);
        }

        await db.SaveChangesAsync(cancellationToken);
        return artifacts.Count;
    }

    private static DataProfileEntity BuildProfile(Guid projectId, ArtifactEntity artifact, int duplicateCount)
    {
        var metrics = new Dictionary<string, object?>
        {
            ["sizeBytes"] = artifact.SizeBytes,
            ["parseStatus"] = artifact.ParseStatus.ToString(),
            ["parser"] = artifact.ParserId,
            ["duplicateCopies"] = Math.Max(0, duplicateCount - 1),
            ["previewCharacters"] = artifact.PreviewText?.Length ?? 0
        };
        var issues = new List<object>();
        var type = artifact.Extension.TrimStart('.').ToUpperInvariant();

        try
        {
            if (artifact.Extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                var lines = ReadLines(artifact, 10_000);
                var rows = lines.Skip(1).Select(x => x.Split(',')).ToArray();
                var headers = lines.FirstOrDefault()?.Split(',') ?? [];
                metrics["rowsSampled"] = rows.Length;
                metrics["columns"] = headers.Length;
                metrics["duplicateRows"] = rows.GroupBy(x => string.Join("\u001f", x)).Count(x => x.Count() > 1);
                metrics["blankCells"] = rows.Sum(row => row.Count(string.IsNullOrWhiteSpace));
                metrics["headers"] = headers;
                if (rows.Any(row => row.Length != headers.Length)) issues.Add(new { code = "ROW_WIDTH_DRIFT", message = "Rows with a different column count were detected." });
            }
            else if (artifact.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                using var document = JsonDocument.Parse(ReadText(artifact));
                var stats = CountJson(document.RootElement, 0);
                metrics["nodes"] = stats.Nodes;
                metrics["maximumDepth"] = stats.Depth;
                metrics["arrays"] = stats.Arrays;
                metrics["objects"] = stats.Objects;
            }
            else if (artifact.Extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
            {
                var document = XDocument.Parse(ReadText(artifact), LoadOptions.None);
                var elements = document.Descendants().ToArray();
                metrics["elements"] = elements.Length;
                metrics["attributes"] = elements.Sum(x => x.Attributes().Count());
                metrics["uniqueElementNames"] = elements.Select(x => x.Name.LocalName).Distinct(StringComparer.Ordinal).Count();
            }
            else if (artifact.Extension is ".log" or ".txt")
            {
                var lines = ReadLines(artifact, 20_000);
                metrics["linesSampled"] = lines.Length;
                metrics["errorLines"] = lines.Count(x => x.Contains("error", StringComparison.OrdinalIgnoreCase));
                metrics["warningLines"] = lines.Count(x => x.Contains("warn", StringComparison.OrdinalIgnoreCase));
            }
            else if (artifact.Extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(artifact.StructureSummaryJson))
            {
                metrics["workbookSummary"] = JsonSerializer.Deserialize<JsonElement>(artifact.StructureSummaryJson);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or System.Xml.XmlException)
        {
            issues.Add(new { code = "PROFILE_READ_FAILED", message = exception.Message });
        }

        if (duplicateCount > 1) issues.Add(new { code = "DUPLICATE_CONTENT", message = $"The same content occurs in {duplicateCount} artifacts." });
        if (artifact.ParseStatus == ArtifactParseStatus.Failed) issues.Add(new { code = "PARSER_FAILED", message = artifact.ParseError ?? "Parser failed." });

        return new DataProfileEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ImportSnapshotId = artifact.ImportSnapshotId,
            ArtifactId = artifact.Id,
            ProfileType = type,
            MetricsJson = JsonSerializer.Serialize(metrics),
            IssuesJson = JsonSerializer.Serialize(issues),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static string ReadText(ArtifactEntity artifact)
    {
        const int maximumCharacters = 2_000_000;
        if (!File.Exists(artifact.StoragePath)) return artifact.PreviewText ?? string.Empty;
        using var reader = new StreamReader(artifact.StoragePath, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[maximumCharacters];
        var read = reader.ReadBlock(buffer, 0, buffer.Length);
        return new string(buffer, 0, read);
    }

    private static string[] ReadLines(ArtifactEntity artifact, int maximumLines) =>
        ReadText(artifact).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Take(maximumLines).ToArray();

    private static (int Nodes, int Depth, int Arrays, int Objects) CountJson(JsonElement element, int depth)
    {
        var nodes = 1; var maximumDepth = depth; var arrays = 0; var objects = 0;
        if (element.ValueKind == JsonValueKind.Object)
        {
            objects++;
            foreach (var property in element.EnumerateObject())
            {
                var child = CountJson(property.Value, depth + 1);
                nodes += child.Nodes; maximumDepth = Math.Max(maximumDepth, child.Depth); arrays += child.Arrays; objects += child.Objects;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            arrays++;
            foreach (var item in element.EnumerateArray())
            {
                var child = CountJson(item, depth + 1);
                nodes += child.Nodes; maximumDepth = Math.Max(maximumDepth, child.Depth); arrays += child.Arrays; objects += child.Objects;
            }
        }
        return (nodes, maximumDepth, arrays, objects);
    }
}
