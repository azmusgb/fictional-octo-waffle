using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkbenchStudio.Api.Data;
using WorkbenchStudio.Api.Domain;

namespace WorkbenchStudio.Api.Services;

public sealed class LineageService(IDbContextFactory<WorkbenchDbContext> factory)
{
    public async Task<int> RebuildAsync(Guid projectId, Guid importId, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var artifacts = await db.Artifacts.AsNoTracking().Where(x => x.ImportSnapshotId == importId).ToListAsync(cancellationToken);
        var old = await db.LineageEdges.Where(x => x.ImportSnapshotId == importId).ToListAsync(cancellationToken);
        db.LineageEdges.RemoveRange(old);
        var edges = new List<LineageEdgeEntity>();

        foreach (var artifact in artifacts)
        {
            if (artifact.ParentArtifactId.HasValue)
                edges.Add(New(projectId, importId, artifact.ParentArtifactId.Value, artifact.Id, "Contains", "Archive contains artifact", null));
        }

        foreach (var group in artifacts.GroupBy(x => x.Sha256).Where(x => x.Count() > 1))
        {
            var first = group.First();
            foreach (var duplicate in group.Skip(1))
                edges.Add(New(projectId, importId, first.Id, duplicate.Id, "DuplicateContent", "Byte-identical SHA-256 content", new { sha256 = group.Key }));
        }

        var candidates = artifacts.Where(x => !string.IsNullOrWhiteSpace(x.PreviewText)).ToArray();
        foreach (var source in candidates)
        {
            foreach (var target in artifacts.Where(x => x.Id != source.Id))
            {
                if ((source.PreviewText?.Contains(target.Name, StringComparison.OrdinalIgnoreCase)).GetValueOrDefault())
                    edges.Add(New(projectId, importId, source.Id, target.Id, "References", $"Preview references {target.Name}", new { target = target.RelativePath }));
            }
        }

        var findings = await db.Findings.AsNoTracking().Where(x => x.ImportSnapshotId == importId && x.ArtifactId != null).ToListAsync(cancellationToken);
        edges.AddRange(findings.Select(f => New(projectId, importId, f.ArtifactId!.Value, null, "FindingEvidence", f.Title, new { f.RuleId, f.SourceLocation, f.Severity })));

        db.LineageEdges.AddRange(edges);
        await db.SaveChangesAsync(cancellationToken);
        return edges.Count;
    }

    private static LineageEdgeEntity New(Guid projectId, Guid importId, Guid from, Guid? to, string type, string label, object? evidence) => new()
    {
        Id = Guid.NewGuid(), ProjectId = projectId, ImportSnapshotId = importId, FromArtifactId = from, ToArtifactId = to,
        EdgeType = type, Label = label, EvidenceJson = evidence is null ? null : JsonSerializer.Serialize(evidence), CreatedAtUtc = DateTimeOffset.UtcNow
    };
}
