using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkbenchStudio.Api.Data;
using WorkbenchStudio.Api.Domain;

namespace WorkbenchStudio.Api.Services;

public sealed record WatchScanResult(bool Changed, Guid? ImportId, string Message, int FileCount, long TotalBytes);

public sealed class WatchFolderService(
    IDbContextFactory<WorkbenchDbContext> factory,
    FileStorageService storage,
    IImportQueue queue,
    ILogger<WatchFolderService> logger)
{
    public async Task<WatchScanResult> ScanAsync(Guid watchFolderId, bool force, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var watch = await db.WatchFolders.SingleOrDefaultAsync(x => x.Id == watchFolderId, cancellationToken);
        if (watch is null)
        {
            throw new KeyNotFoundException("Watch folder was not found.");
        }

        var root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(watch.FolderPath));
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Watch folder does not exist: {root}");
        }

        var ignorePatterns = DeserializeList(watch.IgnorePatternsJson);
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .Where(file => !IsIgnored(Path.GetRelativePath(root, file.FullName), ignorePatterns))
            .OrderBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var fingerprint = ComputeFingerprint(root, files);
        watch.LastScannedAtUtc = DateTimeOffset.UtcNow;
        watch.UpdatedAtUtc = DateTimeOffset.UtcNow;

        if (!force && string.Equals(watch.LastFingerprint, fingerprint, StringComparison.Ordinal))
        {
            await db.SaveChangesAsync(cancellationToken);
            return new WatchScanResult(false, watch.LastImportId, "No changes detected.", files.Length, files.Sum(x => x.Length));
        }

        var importId = Guid.NewGuid();
        var paths = storage.EnsureImportPaths(watch.ProjectId, importId);
        var zipName = FileStorageService.SanitizeFileName($"{watch.Name}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.zip");
        var zipPath = storage.GetSafeDestination(paths.Staging, zipName);

        await using (var stream = new FileStream(zipPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 128 * 1024, true))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = FileStorageService.NormalizeRelativePath(Path.GetRelativePath(root, file.FullName));
                var entry = archive.CreateEntry(relativePath, CompressionLevel.Fastest);
                await using var input = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128 * 1024, true);
                await using var output = entry.Open();
                await input.CopyToAsync(output, cancellationToken);
            }
        }

        var totalBytes = files.Sum(x => x.Length);
        var import = new ImportSnapshotEntity
        {
            Id = importId,
            ProjectId = watch.ProjectId,
            DisplayName = $"Watch: {watch.Name} · {DateTimeOffset.Now:yyyy-MM-dd HH:mm}",
            Status = ImportStatus.Queued,
            CurrentStage = "Queued",
            StatusMessage = $"Watch folder detected {files.Length:N0} file(s) and queued an immutable snapshot.",
            StagingPath = paths.Staging,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            TotalFiles = files.Length,
            TotalBytes = totalBytes
        };

        db.Imports.Add(import);
        watch.LastFingerprint = fingerprint;
        watch.LastImportId = importId;
        await db.SaveChangesAsync(cancellationToken);
        await queue.QueueAsync(importId, cancellationToken);

        logger.LogInformation("Watch folder {WatchFolderId} queued import {ImportId} with {FileCount} files.", watchFolderId, importId, files.Length);
        return new WatchScanResult(true, importId, "Changes detected and snapshot queued.", files.Length, totalBytes);
    }

    private static string ComputeFingerprint(string root, IEnumerable<FileInfo> files)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files)
        {
            var relative = FileStorageService.NormalizeRelativePath(Path.GetRelativePath(root, file.FullName));
            var line = $"{relative}\0{file.Length}\0{file.LastWriteTimeUtc.Ticks}\n";
            hash.AppendData(Encoding.UTF8.GetBytes(line));
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static IReadOnlyList<string> DeserializeList(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static bool IsIgnored(string relativePath, IReadOnlyList<string> patterns)
    {
        var normalized = FileStorageService.NormalizeRelativePath(relativePath);
        return patterns.Any(pattern => WildcardMatch(normalized, pattern));
    }

    private static bool WildcardMatch(string value, string pattern)
    {
        var escaped = System.Text.RegularExpressions.Regex.Escape(pattern.Replace('\\', '/'))
            .Replace("\\*\\*", ".*")
            .Replace("\\*", "[^/]*")
            .Replace("\\?", ".");
        return System.Text.RegularExpressions.Regex.IsMatch(value, $"^{escaped}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}

public sealed class WatchFolderWorkerService(
    IDbContextFactory<WorkbenchDbContext> factory,
    WatchFolderService service,
    ILogger<WatchFolderWorkerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await using var db = await factory.CreateDbContextAsync(stoppingToken);
            var now = DateTimeOffset.UtcNow;
            var due = await db.WatchFolders.AsNoTracking()
                .Where(x => x.Enabled && !x.RequireApproval && x.TriggerMode != WatchTriggerMode.Manual)
                .Where(x => x.LastScannedAtUtc == null || x.LastScannedAtUtc.Value.AddMinutes(x.ScanIntervalMinutes) <= now)
                .Select(x => x.Id)
                .ToListAsync(stoppingToken);

            foreach (var id in due)
            {
                try { await service.ScanAsync(id, force: false, stoppingToken); }
                catch (Exception exception) { logger.LogWarning(exception, "Watch scan failed for {WatchFolderId}.", id); }
            }
        }
    }
}
