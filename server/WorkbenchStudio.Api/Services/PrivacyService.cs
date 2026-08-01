using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WorkbenchStudio.Api.Data;
using WorkbenchStudio.Api.Domain;

namespace WorkbenchStudio.Api.Services;

public sealed class PrivacyService(IDbContextFactory<WorkbenchDbContext> factory, FileStorageService storage)
{
    private static readonly (string Kind, string Severity, Regex Pattern)[] Patterns =
    [
        ("Social Security number", "Restricted", new Regex(@"(?<!\d)\d{3}-\d{2}-\d{4}(?!\d)", RegexOptions.Compiled)),
        ("Email address", "Confidential", new Regex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Phone number", "Confidential", new Regex(@"(?<!\d)(?:\+?1[-.\s]?)?(?:\(?\d{3}\)?[-.\s]?)\d{3}[-.\s]?\d{4}(?!\d)", RegexOptions.Compiled)),
        ("Payment card candidate", "Restricted", new Regex(@"(?<!\d)(?:\d[ -]*?){13,19}(?!\d)", RegexOptions.Compiled)),
        ("API key candidate", "Restricted", new Regex(@"\b(?:sk|api|token)[-_][A-Za-z0-9_-]{16,}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled))
    ];

    public async Task<int> ScanAsync(Guid projectId, Guid importId, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var artifacts = await db.Artifacts.AsNoTracking().Where(x => x.ImportSnapshotId == importId).ToListAsync(cancellationToken);
        var old = await db.PrivacyDetections.Where(x => x.ImportSnapshotId == importId).ToListAsync(cancellationToken);
        db.PrivacyDetections.RemoveRange(old);
        var count = 0;

        foreach (var artifact in artifacts.Where(IsTextArtifact))
        {
            var text = ReadText(artifact, 2_000_000);
            foreach (var (kind, severity, pattern) in Patterns)
            {
                foreach (Match match in pattern.Matches(text).Cast<Match>().Take(500))
                {
                    var line = 1 + CountNewlines(text, match.Index);
                    db.PrivacyDetections.Add(new PrivacyDetectionEntity
                    {
                        Id = Guid.NewGuid(), ProjectId = projectId, ImportSnapshotId = importId, ArtifactId = artifact.Id,
                        Kind = kind, Severity = severity, SourceLocation = $"line {line}", MaskedPreview = Mask(match.Value), Status = "Open",
                        CreatedAtUtc = DateTimeOffset.UtcNow
                    });
                    count++;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return count;
    }

    public async Task<(string Path, string FileName)> CreateRedactedExportAsync(Guid projectId, Guid importId, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var artifacts = await db.Artifacts.AsNoTracking().Where(x => x.ImportSnapshotId == importId).ToListAsync(cancellationToken);
        var paths = storage.EnsureImportPaths(projectId, importId);
        var fileName = $"redacted-evidence-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.zip";
        var destination = Path.Combine(paths.Exports, fileName);

        await using var stream = new FileStream(destination, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 128 * 1024, true);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);
        foreach (var artifact in artifacts.Where(IsTextArtifact))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = ReadText(artifact, 10_000_000);
            foreach (var (_, _, pattern) in Patterns) text = pattern.Replace(text, match => Mask(match.Value));
            var entry = archive.CreateEntry(artifact.RelativePath, CompressionLevel.Fastest);
            await using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync(text.AsMemory(), cancellationToken);
        }

        return (destination, fileName);
    }

    private static bool IsTextArtifact(ArtifactEntity artifact) => artifact.Extension.ToLowerInvariant() is ".json" or ".csv" or ".xml" or ".txt" or ".log" or ".md" or ".html" or ".htm";
    private static string ReadText(ArtifactEntity artifact, int maximumCharacters)
    {
        if (!File.Exists(artifact.StoragePath))
        {
            var preview = artifact.PreviewText ?? string.Empty;
            return preview.Length > maximumCharacters ? preview[..maximumCharacters] : preview;
        }
        using var reader = new StreamReader(artifact.StoragePath, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[maximumCharacters];
        var read = reader.ReadBlock(buffer, 0, buffer.Length);
        return new string(buffer, 0, read);
    }
    private static int CountNewlines(string value, int endIndex)
    {
        var count = 0;
        for (var index = 0; index < endIndex; index++) if (value[index] == '\n') count++;
        return count;
    }

    private static string Mask(string value) => value.Length <= 4 ? new string('•', value.Length) : value[..2] + new string('•', Math.Min(12, value.Length - 4)) + value[^2..];
}
