using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WorkbenchStudio.Api.Data;
using WorkbenchStudio.Api.Domain;
using WorkbenchStudio.Api.Parsing;

namespace WorkbenchStudio.Api.Services;

public sealed class ImportProcessor(
    IDbContextFactory<WorkbenchDbContext> dbContextFactory,
    FileStorageService storage,
    HashingService hashing,
    ParserRegistry parserRegistry,
    IOptions<WorkspaceOptions> options,
    ILogger<ImportProcessor> logger)
{
    private readonly WorkspaceOptions _limits = options.Value;

    public async Task ProcessAsync(Guid importId, CancellationToken stoppingToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(stoppingToken);
        var import = await db.Imports.SingleOrDefaultAsync(x => x.Id == importId, stoppingToken);
        if (import is null)
        {
            logger.LogWarning("Import {ImportId} no longer exists.", importId);
            return;
        }

        if (import.Status is ImportStatus.Completed or ImportStatus.CompletedWithWarnings or ImportStatus.Cancelled)
        {
            return;
        }

        var paths = storage.EnsureImportPaths(import.ProjectId, import.Id);
        import.StagingPath ??= paths.Staging;
        import.StartedAtUtc ??= DateTimeOffset.UtcNow;
        import.Status = ImportStatus.Preparing;
        import.CurrentStage = "Preparing";
        import.StatusMessage = "Preparing the import workspace.";
        await db.SaveChangesAsync(stoppingToken);

        try
        {
            await ThrowIfCancellationRequestedAsync(db, import, stoppingToken);
            await ClearPreviousAttemptAsync(db, import.Id, paths, stoppingToken);

            var stagedFiles = Directory.Exists(paths.Staging)
                ? Directory.EnumerateFiles(paths.Staging, "*", SearchOption.TopDirectoryOnly).Order().ToArray()
                : Array.Empty<string>();

            if (stagedFiles.Length == 0)
            {
                throw new InvalidOperationException("No staged files were available for processing.");
            }

            var artifacts = new List<ArtifactEntity>();
            var usedRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var extractedBytes = 0L;
            var extractedFileCount = 0;

            import.Status = ImportStatus.Extracting;
            import.CurrentStage = "Extracting";
            import.StatusMessage = "Copying originals and safely extracting archives.";
            await db.SaveChangesAsync(stoppingToken);

            foreach (var stagedFile in stagedFiles)
            {
                await ThrowIfCancellationRequestedAsync(db, import, stoppingToken);

                var safeName = FileStorageService.SanitizeFileName(Path.GetFileName(stagedFile));
                var originalDestination = storage.GetSafeDestination(paths.Originals, safeName);
                Directory.CreateDirectory(Path.GetDirectoryName(originalDestination)!);
                File.Copy(stagedFile, originalDestination, overwrite: true);

                var originalRelative = MakeUniqueRelativePath(
                    FileStorageService.NormalizeRelativePath($"originals/{safeName}"),
                    usedRelativePaths);
                var originalArtifact = await BuildArtifactAsync(
                    import.Id,
                    null,
                    originalDestination,
                    originalRelative,
                    stoppingToken);
                artifacts.Add(originalArtifact);

                if (Path.GetExtension(stagedFile).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    originalArtifact.ParseStatus = ArtifactParseStatus.Unsupported;
                    originalArtifact.ParserId = "archive-container";
                    var archiveFolder = Path.GetFileNameWithoutExtension(safeName);
                    var extracted = await ExtractZipAsync(
                        stagedFile,
                        paths.Extracted,
                        archiveFolder,
                        import,
                        db,
                        usedRelativePaths,
                        originalArtifact.Id,
                        extractedBytes,
                        extractedFileCount,
                        stoppingToken);
                    extractedBytes = extracted.TotalBytes;
                    extractedFileCount = extracted.FileCount;
                    artifacts.AddRange(extracted.Artifacts);
                }
            }

            db.Artifacts.AddRange(artifacts);
            import.TotalFiles = artifacts.Count;
            import.TotalBytes = artifacts.Sum(x => x.SizeBytes);
            import.Status = ImportStatus.Inventorying;
            import.CurrentStage = "Inventorying";
            import.StatusMessage = $"Inventoried {artifacts.Count:N0} artifacts.";
            await db.SaveChangesAsync(stoppingToken);

            await AddInventoryFindingsAsync(db, import, artifacts, stoppingToken);

            import.Status = ImportStatus.Parsing;
            import.CurrentStage = "Parsing";
            import.StatusMessage = "Parsing supported artifacts.";
            import.ProcessedFiles = 0;
            await db.SaveChangesAsync(stoppingToken);

            foreach (var artifact in artifacts.OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase))
            {
                await ThrowIfCancellationRequestedAsync(db, import, stoppingToken);
                await ParseArtifactAsync(db, import, artifact, stoppingToken);
                import.ProcessedFiles++;
                if (import.ProcessedFiles % 10 == 0 || import.ProcessedFiles == import.TotalFiles)
                {
                    import.StatusMessage = $"Parsed {import.ProcessedFiles:N0} of {import.TotalFiles:N0} artifacts.";
                    await db.SaveChangesAsync(stoppingToken);
                }
            }

            import.Status = ImportStatus.Validating;
            import.CurrentStage = "Validating";
            import.StatusMessage = "Consolidating validation findings.";
            await db.SaveChangesAsync(stoppingToken);

            import.WarningCount = await db.Findings.CountAsync(
                x => x.ImportSnapshotId == import.Id && x.Severity == FindingSeverity.Warning,
                stoppingToken);
            import.ErrorCount = await db.Findings.CountAsync(
                x => x.ImportSnapshotId == import.Id && x.Severity == FindingSeverity.Error,
                stoppingToken);

            import.Status = ImportStatus.Indexing;
            import.CurrentStage = "Indexing";
            import.StatusMessage = "Finalizing searchable metadata.";
            await db.SaveChangesAsync(stoppingToken);

            import.Status = import.WarningCount > 0 || import.ErrorCount > 0
                ? ImportStatus.CompletedWithWarnings
                : ImportStatus.Completed;
            import.CurrentStage = import.Status.ToString();
            import.StatusMessage = import.Status == ImportStatus.Completed
                ? "Import completed successfully."
                : $"Import completed with {import.WarningCount:N0} warnings and {import.ErrorCount:N0} errors.";
            import.CompletedAtUtc = DateTimeOffset.UtcNow;

            var project = await db.Projects.FindAsync([import.ProjectId], stoppingToken);
            if (project is not null)
            {
                project.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync(stoppingToken);
        }
        catch (ImportCancellationException)
        {
            import.Status = ImportStatus.Cancelled;
            import.CurrentStage = "Cancelled";
            import.StatusMessage = "Import cancelled by the user.";
            import.CompletedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Import {ImportId} failed.", import.Id);
            import.Status = ImportStatus.Failed;
            import.CurrentStage = "Failed";
            import.StatusMessage = exception.Message;
            import.ErrorCount++;
            import.CompletedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    private async Task<ExtractionResult> ExtractZipAsync(
        string zipPath,
        string extractionRoot,
        string archiveFolder,
        ImportSnapshotEntity import,
        WorkbenchDbContext db,
        HashSet<string> usedRelativePaths,
        Guid parentArtifactId,
        long currentExtractedBytes,
        int currentFileCount,
        CancellationToken cancellationToken)
    {
        var result = new List<ArtifactEntity>();
        using var archive = ZipFile.OpenRead(zipPath);

        foreach (var entry in archive.Entries)
        {
            await ThrowIfCancellationRequestedAsync(db, import, cancellationToken);

            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            currentFileCount++;
            if (currentFileCount > _limits.MaximumExtractedFiles)
            {
                throw new InvalidDataException($"Archive extraction exceeded the {_limits.MaximumExtractedFiles:N0}-file safety limit.");
            }

            if (entry.Length > _limits.MaximumSingleFileBytes)
            {
                await AddFindingAsync(
                    db,
                    import,
                    null,
                    FindingSeverity.Warning,
                    "ZIP_ENTRY_TOO_LARGE",
                    "Archive entry skipped",
                    $"{entry.FullName} exceeds the per-file extraction limit.",
                    entry.FullName,
                    null,
                    "Reduce the file size or raise the configured limit after reviewing resource requirements.",
                    cancellationToken);
                continue;
            }

            currentExtractedBytes = checked(currentExtractedBytes + entry.Length);
            if (currentExtractedBytes > _limits.MaximumExtractedBytes)
            {
                throw new InvalidDataException("Archive extraction exceeded the total extracted-size safety limit.");
            }

            if (entry.CompressedLength > 0)
            {
                var ratio = (double)entry.Length / entry.CompressedLength;
                if (ratio > _limits.MaximumCompressionRatio)
                {
                    await AddFindingAsync(
                        db,
                        import,
                        null,
                        FindingSeverity.Warning,
                        "ZIP_COMPRESSION_RATIO",
                        "Suspicious archive entry skipped",
                        $"{entry.FullName} has a compression ratio of {ratio:N1}:1.",
                        entry.FullName,
                        null,
                        "Validate the archive source and content before increasing the compression-ratio limit.",
                        cancellationToken);
                    continue;
                }
            }

            var normalizedEntry = FileStorageService.NormalizeRelativePath(entry.FullName);
            var relativePath = MakeUniqueRelativePath(
                FileStorageService.NormalizeRelativePath($"extracted/{archiveFolder}/{normalizedEntry}"),
                usedRelativePaths);
            var storageRelative = relativePath["extracted/".Length..];
            var destination = storage.GetSafeDestination(extractionRoot, storageRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            await using (var input = entry.Open())
            await using (var output = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await input.CopyToAsync(output, 128 * 1024, cancellationToken);
            }

            result.Add(await BuildArtifactAsync(
                import.Id,
                parentArtifactId,
                destination,
                relativePath,
                cancellationToken));
        }

        return new ExtractionResult(result, currentExtractedBytes, currentFileCount);
    }

    private async Task<ArtifactEntity> BuildArtifactAsync(
        Guid importId,
        Guid? parentArtifactId,
        string storagePath,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(storagePath);
        var extension = info.Extension.ToLowerInvariant();
        return new ArtifactEntity
        {
            Id = Guid.NewGuid(),
            ImportSnapshotId = importId,
            ParentArtifactId = parentArtifactId,
            Name = info.Name,
            RelativePath = relativePath,
            StoragePath = storagePath,
            Extension = extension,
            MediaType = GetMediaType(extension),
            SizeBytes = info.Length,
            Sha256 = await hashing.ComputeSha256Async(storagePath, cancellationToken),
            ParseStatus = ArtifactParseStatus.Pending,
            ImportedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private async Task ParseArtifactAsync(
        WorkbenchDbContext db,
        ImportSnapshotEntity import,
        ArtifactEntity artifact,
        CancellationToken cancellationToken)
    {
        if (artifact.SizeBytes == 0)
        {
            await AddFindingAsync(
                db,
                import,
                artifact,
                FindingSeverity.Info,
                "ARTIFACT_EMPTY",
                "Empty artifact",
                "The artifact contains zero bytes.",
                artifact.RelativePath,
                null,
                "Confirm whether the empty artifact is expected.",
                cancellationToken);
        }

        var context = new ArtifactParseContext(
            artifact.StoragePath,
            artifact.RelativePath,
            artifact.Extension,
            artifact.SizeBytes);
        var parser = parserRegistry.Resolve(context);
        if (parser is null)
        {
            artifact.ParseStatus = ArtifactParseStatus.Unsupported;
            artifact.ParserId = null;
            return;
        }

        ArtifactParseResult result;
        try
        {
            result = await parser.ParseAsync(context, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            result = new ArtifactParseResult(
                ArtifactParseStatus.Failed,
                parser.ParserId,
                parser.ParserVersion,
                null,
                null,
                [new ParserFinding(
                    FindingSeverity.Error,
                    "PARSER_UNHANDLED_ERROR",
                    "Parser failed unexpectedly",
                    exception.Message,
                    artifact.RelativePath,
                    Recommendation: "Review the parser logs and retain the original artifact for diagnosis.")],
                exception.Message);
        }

        artifact.ParseStatus = result.Status;
        artifact.ParserId = result.ParserId;
        artifact.ParserVersion = result.ParserVersion;
        artifact.StructureSummaryJson = result.StructureSummary is null
            ? null
            : JsonSerializer.Serialize(result.StructureSummary);
        artifact.PreviewText = result.PreviewText;
        artifact.ParseError = result.Error;

        foreach (var finding in result.Findings)
        {
            await AddFindingAsync(
                db,
                import,
                artifact,
                finding.Severity,
                finding.RuleId,
                finding.Title,
                finding.Message,
                finding.SourceLocation,
                finding.EvidenceExcerpt,
                finding.Recommendation,
                cancellationToken);
        }
    }

    private static async Task AddInventoryFindingsAsync(
        WorkbenchDbContext db,
        ImportSnapshotEntity import,
        IReadOnlyCollection<ArtifactEntity> artifacts,
        CancellationToken cancellationToken)
    {
        var duplicateGroups = artifacts
            .GroupBy(x => x.Sha256, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToArray();

        foreach (var group in duplicateGroups)
        {
            var paths = group.Select(x => x.RelativePath).Order(StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var artifact in group)
            {
                await AddFindingAsync(
                    db,
                    import,
                    artifact,
                    FindingSeverity.Info,
                    "DUPLICATE_CONTENT",
                    "Duplicate artifact content",
                    $"This content hash occurs in {paths.Length:N0} artifacts.",
                    artifact.RelativePath,
                    string.Join("; ", paths.Take(10)),
                    "Review whether duplicate content is intentional.",
                    cancellationToken);
            }
        }

        var caseCollisions = artifacts
            .GroupBy(x => x.RelativePath.ToUpperInvariant(), StringComparer.Ordinal)
            .Where(group => group.Select(x => x.RelativePath).Distinct(StringComparer.Ordinal).Count() > 1);

        foreach (var group in caseCollisions)
        {
            var paths = group.Select(x => x.RelativePath).ToArray();
            foreach (var artifact in group)
            {
                await AddFindingAsync(
                    db,
                    import,
                    artifact,
                    FindingSeverity.Warning,
                    "PATH_CASE_COLLISION",
                    "Case-sensitive path collision",
                    "Multiple artifact paths differ only by letter casing.",
                    artifact.RelativePath,
                    string.Join("; ", paths),
                    "Rename files so their paths remain unique on case-insensitive file systems.",
                    cancellationToken);
            }
        }
    }

    private static Task AddFindingAsync(
        WorkbenchDbContext db,
        ImportSnapshotEntity import,
        ArtifactEntity? artifact,
        FindingSeverity severity,
        string ruleId,
        string title,
        string message,
        string? sourceLocation,
        string? evidenceExcerpt,
        string? recommendation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        db.Findings.Add(new FindingEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = import.ProjectId,
            ImportSnapshotId = import.Id,
            ArtifactId = artifact?.Id,
            Severity = severity,
            RuleId = ruleId,
            Title = title,
            Message = message,
            SourceLocation = sourceLocation,
            EvidenceExcerpt = evidenceExcerpt,
            Recommendation = recommendation,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        return Task.CompletedTask;
    }

    private static async Task ThrowIfCancellationRequestedAsync(
        WorkbenchDbContext db,
        ImportSnapshotEntity import,
        CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();
        await db.Entry(import).ReloadAsync(stoppingToken);
        if (import.CancellationRequested)
        {
            throw new ImportCancellationException();
        }
    }

    private static async Task ClearPreviousAttemptAsync(
        WorkbenchDbContext db,
        Guid importId,
        ImportPaths paths,
        CancellationToken cancellationToken)
    {
        var previousArtifacts = await db.Artifacts
            .Where(x => x.ImportSnapshotId == importId)
            .ToListAsync(cancellationToken);
        var previousFindings = await db.Findings
            .Where(x => x.ImportSnapshotId == importId)
            .ToListAsync(cancellationToken);
        db.Findings.RemoveRange(previousFindings);
        db.Artifacts.RemoveRange(previousArtifacts);
        await db.SaveChangesAsync(cancellationToken);

        RecreateDirectory(paths.Originals);
        RecreateDirectory(paths.Extracted);
        RecreateDirectory(paths.Exports);
    }

    private static void RecreateDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
    }

    private static string MakeUniqueRelativePath(string requested, HashSet<string> used)
    {
        var normalized = FileStorageService.NormalizeRelativePath(requested);
        if (used.Add(normalized))
        {
            return normalized;
        }

        var directory = Path.GetDirectoryName(normalized)?.Replace('\\', '/') ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(normalized);
        var extension = Path.GetExtension(normalized);
        for (var index = 2; ; index++)
        {
            var candidateFile = $"{fileName} ({index}){extension}";
            var candidate = string.IsNullOrEmpty(directory) ? candidateFile : $"{directory}/{candidateFile}";
            if (used.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static string GetMediaType(string extension) => extension.ToLowerInvariant() switch
    {
        ".json" => "application/json",
        ".csv" => "text/csv",
        ".xml" => "application/xml",
        ".txt" or ".log" or ".out" or ".trace" => "text/plain",
        ".zip" => "application/zip",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".xls" => "application/vnd.ms-excel",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream"
    };

    private sealed record ExtractionResult(
        IReadOnlyList<ArtifactEntity> Artifacts,
        long TotalBytes,
        int FileCount);

    private sealed class ImportCancellationException : Exception
    {
    }
}
