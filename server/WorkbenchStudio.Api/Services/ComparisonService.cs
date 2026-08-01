using Microsoft.EntityFrameworkCore;
using WorkbenchStudio.Api.Data;
using WorkbenchStudio.Api.Domain;

namespace WorkbenchStudio.Api.Services;

public sealed class ComparisonService(IDbContextFactory<WorkbenchDbContext> dbContextFactory)
{
    public async Task<CompareResultDto?> CompareAsync(
        Guid projectId,
        Guid leftImportId,
        Guid rightImportId,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var importCount = await db.Imports.CountAsync(
            x => x.ProjectId == projectId && (x.Id == leftImportId || x.Id == rightImportId),
            cancellationToken);
        if (importCount != 2 || leftImportId == rightImportId)
        {
            return null;
        }

        var left = await db.Artifacts
            .Where(x => x.ImportSnapshotId == leftImportId)
            .Select(x => new ArtifactProjection(x.RelativePath, x.Sha256, x.SizeBytes))
            .ToListAsync(cancellationToken);
        var right = await db.Artifacts
            .Where(x => x.ImportSnapshotId == rightImportId)
            .Select(x => new ArtifactProjection(x.RelativePath, x.Sha256, x.SizeBytes))
            .ToListAsync(cancellationToken);

        var leftMap = left.ToDictionary(x => x.RelativePath, StringComparer.OrdinalIgnoreCase);
        var rightMap = right.ToDictionary(x => x.RelativePath, StringComparer.OrdinalIgnoreCase);
        var allPaths = leftMap.Keys
            .Union(rightMap.Keys, StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var differences = new List<ArtifactDifferenceDto>(allPaths.Length);
        foreach (var path in allPaths)
        {
            leftMap.TryGetValue(path, out var leftArtifact);
            rightMap.TryGetValue(path, out var rightArtifact);

            var changeType = (leftArtifact, rightArtifact) switch
            {
                (null, not null) => "Added",
                (not null, null) => "Removed",
                (not null, not null) when leftArtifact.Sha256 == rightArtifact.Sha256 => "Unchanged",
                _ => "Modified"
            };

            differences.Add(new ArtifactDifferenceDto(
                path,
                changeType,
                leftArtifact?.Sha256,
                rightArtifact?.Sha256,
                leftArtifact?.SizeBytes,
                rightArtifact?.SizeBytes));
        }

        return new CompareResultDto(
            leftImportId,
            rightImportId,
            differences.Count(x => x.ChangeType == "Added"),
            differences.Count(x => x.ChangeType == "Removed"),
            differences.Count(x => x.ChangeType == "Modified"),
            differences.Count(x => x.ChangeType == "Unchanged"),
            differences);
    }

    private sealed record ArtifactProjection(string RelativePath, string Sha256, long SizeBytes);
}
