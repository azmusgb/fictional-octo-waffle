using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkbenchStudio.Api.Data;
using WorkbenchStudio.Api.Domain;

namespace WorkbenchStudio.Api.Services;

public sealed class CommandIntelligenceService(
    IDbContextFactory<WorkbenchDbContext> dbContextFactory,
    FileStorageService storage,
    DecisionOperationsService decisionOperations)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static readonly IReadOnlyDictionary<string, double> DefaultWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
    {
        ["Error findings"] = 1.35,
        ["Warning findings"] = 1.05,
        ["Informational findings"] = 0.35,
        ["Sensitive-value candidates"] = 1.25,
        ["Lineage impact"] = 1.10,
        ["Parser failure"] = 1.30,
        ["Unsupported format"] = 0.65,
        ["Review state"] = 0.85
    };

    public async Task<IReadOnlyList<QueuePolicyDto>> GetQueuePoliciesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.QueuePolicies.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.Active).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<QueuePolicyDto> CreateQueuePolicyAsync(Guid projectId, CreateQueuePolicyRequest request, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Projects.AnyAsync(x => x.Id == projectId, cancellationToken)) throw new KeyNotFoundException("Project was not found.");
        var now = DateTimeOffset.UtcNow;
        var weights = request.Weights is { Count: > 0 } ? request.Weights : DefaultWeights.Select(x => new QueueWeightDto(x.Key, x.Value, $"Multiplier applied to {x.Key.ToLowerInvariant()}.")).ToList();
        if (request.Active) await db.QueuePolicies.Where(x => x.ProjectId == projectId && x.Active).ExecuteUpdateAsync(x => x.SetProperty(p => p.Active, false), cancellationToken);
        var entity = new QueuePolicyEntity
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Name = string.IsNullOrWhiteSpace(request.Name) ? "Adaptive evidence queue" : request.Name.Trim(),
            WeightsJson = JsonSerializer.Serialize(weights, JsonOptions), SlaHours = Math.Clamp(request.SlaHours, 1, 720), Active = request.Active,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.QueuePolicies.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<QueuePolicyDto> UpdateQueuePolicyAsync(Guid projectId, Guid policyId, UpdateQueuePolicyRequest request, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.QueuePolicies.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == policyId, cancellationToken)
            ?? throw new KeyNotFoundException("Queue policy was not found.");
        if (!string.IsNullOrWhiteSpace(request.Name)) entity.Name = request.Name.Trim();
        if (request.SlaHours.HasValue) entity.SlaHours = Math.Clamp(request.SlaHours.Value, 1, 720);
        if (request.Weights is { Count: > 0 }) entity.WeightsJson = JsonSerializer.Serialize(request.Weights, JsonOptions);
        if (request.Active.HasValue)
        {
            if (request.Active.Value) await db.QueuePolicies.Where(x => x.ProjectId == projectId && x.Id != policyId && x.Active).ExecuteUpdateAsync(x => x.SetProperty(p => p.Active, false), cancellationToken);
            entity.Active = request.Active.Value;
        }
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<AdaptiveQueueItemDto>> GetAdaptiveQueueAsync(Guid projectId, Guid importId, Guid? policyId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var triage = await decisionOperations.GetTriageAsync(projectId, importId, cancellationToken);
        var policy = policyId.HasValue
            ? await db.QueuePolicies.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == policyId.Value, cancellationToken)
            : await db.QueuePolicies.AsNoTracking().Where(x => x.ProjectId == projectId && x.Active).OrderByDescending(x => x.UpdatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        var weights = policy is null ? DefaultWeights : DeserializeWeights(policy.WeightsJson);
        var slaHours = policy?.SlaHours ?? 24;
        var artifacts = await db.Artifacts.AsNoTracking().Where(x => x.ImportSnapshotId == importId).ToDictionaryAsync(x => x.Id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        return triage.Select(item =>
        {
            var reasons = item.Factors.Select(factor =>
            {
                var multiplier = weights.GetValueOrDefault(factor.Name, 1d);
                var points = (int)Math.Round(factor.Points * multiplier);
                return new QueueReasonDto(factor.Name, points, $"{factor.Explanation}; policy multiplier {multiplier:0.##}×.");
            }).ToList();
            var importedAt = artifacts.GetValueOrDefault(item.ArtifactId)?.ImportedAtUtc ?? now;
            var dueAt = importedAt.AddHours(slaHours);
            var remaining = dueAt - now;
            var slaState = remaining <= TimeSpan.Zero ? "Overdue" : remaining <= TimeSpan.FromHours(Math.Max(2, slaHours * .2)) ? "DueSoon" : "OnTrack";
            if (slaState == "Overdue") reasons.Add(new QueueReasonDto("SLA breach", 18, $"Review exceeded the {slaHours}-hour policy window."));
            else if (slaState == "DueSoon") reasons.Add(new QueueReasonDto("SLA proximity", 8, "Review is approaching its policy deadline."));
            var score = Math.Min(100, reasons.Sum(x => x.Points));
            var band = score switch { >= 75 => "Critical", >= 50 => "High", >= 25 => "Medium", _ => "Low" };
            return new AdaptiveQueueItemDto(item.ArtifactId, item.ArtifactPath, score, band, item.ReviewStatus, dueAt, slaState, reasons.OrderByDescending(x => x.Points).ToList());
        }).OrderByDescending(x => x.Score).ThenBy(x => x.DueAtUtc).ThenBy(x => x.ArtifactPath).Take(150).Select((item, index) => item with { Rank = index + 1 }).ToList();
    }

    public async Task<IReadOnlyList<ScenarioRunDto>> GetScenariosAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.ScenarioRuns.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.CreatedAtUtc).Take(50).ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ScenarioRunDto> RunScenarioAsync(Guid projectId, CreateScenarioRunRequest request, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Imports.AnyAsync(x => x.ProjectId == projectId && x.Id == request.ImportId, cancellationToken)) throw new KeyNotFoundException("Snapshot was not found.");
        var current = await GetSnapshotMetricsAsync(db, projectId, request.ImportId, cancellationToken);
        var projected = new Dictionary<string, double>(current, StringComparer.OrdinalIgnoreCase);
        foreach (var assumption in request.Assumptions) projected[assumption.Metric] = Math.Max(0, projected.GetValueOrDefault(assumption.Metric) + assumption.Delta);
        var currentScore = ReadinessScore(current);
        var projectedScore = ReadinessScore(projected);
        var result = new ScenarioResultDto(currentScore, projectedScore, projectedScore - currentScore,
            projectedScore >= 90 ? "Release candidate" : projectedScore >= 75 ? "Conditionally ready" : projectedScore >= 55 ? "Material risk remains" : "Not ready",
            current.Select(metric => new ScenarioMetricDto(metric.Key, metric.Value, projected.GetValueOrDefault(metric.Key), projected.GetValueOrDefault(metric.Key) - metric.Value)).ToList(),
            BuildScenarioRecommendations(projected));
        var entity = new ScenarioRunEntity
        {
            Id = Guid.NewGuid(), ProjectId = projectId, ImportSnapshotId = request.ImportId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? "Readiness scenario" : request.Name.Trim(),
            AssumptionsJson = JsonSerializer.Serialize(request.Assumptions, JsonOptions), ResultJson = JsonSerializer.Serialize(result, JsonOptions), CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.ScenarioRuns.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<ApprovalGateDto>> GetApprovalGatesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.ApprovalGates.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ApprovalGateDto> CreateApprovalGateAsync(Guid projectId, CreateApprovalGateRequest request, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Imports.AnyAsync(x => x.ProjectId == projectId && x.Id == request.ImportId, cancellationToken)) throw new KeyNotFoundException("Snapshot was not found.");
        var requirements = await EvaluateApprovalRequirementsAsync(db, projectId, request.ImportId, cancellationToken);
        var entity = new ApprovalGateEntity
        {
            Id = Guid.NewGuid(), ProjectId = projectId, ImportSnapshotId = request.ImportId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? "Release readiness approval" : request.Name.Trim(),
            GateType = string.IsNullOrWhiteSpace(request.GateType) ? "Release" : request.GateType.Trim(),
            RequiredRole = string.IsNullOrWhiteSpace(request.RequiredRole) ? "Reviewer" : request.RequiredRole.Trim(),
            Status = "Pending", RequirementsJson = JsonSerializer.Serialize(requirements, JsonOptions), CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.ApprovalGates.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<ApprovalGateDto> DecideApprovalGateAsync(Guid projectId, Guid gateId, ApprovalDecisionRequest request, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.ApprovalGates.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == gateId, cancellationToken)
            ?? throw new KeyNotFoundException("Approval gate was not found.");
        var decision = request.Decision.Trim().ToLowerInvariant() switch { "approve" or "approved" => "Approved", "reject" or "rejected" => "Rejected", _ => throw new ArgumentException("Decision must be Approve or Reject.") };
        var requirements = DeserializeRequirements(entity.RequirementsJson);
        if (decision == "Approved" && requirements.Any(x => !x.Passed)) throw new InvalidOperationException("The gate cannot be approved while required controls are failing.");
        entity.Status = decision;
        entity.DecidedBy = string.IsNullOrWhiteSpace(request.DecidedBy) ? "Local reviewer" : request.DecidedBy.Trim();
        entity.Rationale = request.Rationale?.Trim();
        entity.DecidedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<AnomalyExplanationDto>> GetAnomalyExplanationsAsync(Guid projectId, Guid importId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var findings = await db.Findings.AsNoTracking().Where(x => x.ProjectId == projectId && x.ImportSnapshotId == importId).OrderByDescending(x => x.Severity).ThenBy(x => x.ArtifactId).ToListAsync(cancellationToken);
        var artifactPaths = await db.Artifacts.AsNoTracking().Where(x => x.ImportSnapshotId == importId).ToDictionaryAsync(x => x.Id, x => x.RelativePath, cancellationToken);
        var grouped = findings.GroupBy(x => x.ArtifactId).Take(30);
        return grouped.Select(group =>
        {
            var first = group.First();
            var errors = group.Count(x => x.Severity == FindingSeverity.Error);
            var warnings = group.Count(x => x.Severity == FindingSeverity.Warning);
            var observed = string.Join(" ", group.Take(3).Select(x => x.Message));
            var expected = string.Join(" ", group.Select(x => x.Recommendation).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(2));
            var drivers = group.GroupBy(x => x.RuleId).Select(x => $"{x.Key} ({x.Count()})").Take(5).ToList();
            var evidence = group.Take(6).Select(x => new AnomalyEvidenceDto(x.Id, x.ArtifactId, x.SourceLocation, x.EvidenceExcerpt, x.RuleId)).ToList();
            var severity = errors > 0 ? "Critical" : warnings > 1 ? "High" : "Medium";
            return new AnomalyExplanationDto(group.Key, group.Key.HasValue ? artifactPaths.GetValueOrDefault(group.Key.Value, "Unknown artifact") : "Project-level", severity,
                first.Title, observed, string.IsNullOrWhiteSpace(expected) ? "Evidence should satisfy the associated deterministic parser or validation rule." : expected,
                drivers, evidence, errors > 0 ? "Blocks approval until resolved or explicitly rejected." : "Raises review priority and may affect downstream outputs.",
                first.Recommendation ?? "Inspect the cited source evidence and record a human review decision.");
        }).ToList();
    }

    public async Task<ExecutiveSummaryDto> GetExecutiveSummaryAsync(Guid projectId, Guid importId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var metrics = await GetSnapshotMetricsAsync(db, projectId, importId, cancellationToken);
        var queue = await GetAdaptiveQueueAsync(projectId, importId, null, cancellationToken);
        var pendingApprovals = await db.ApprovalGates.AsNoTracking().CountAsync(x => x.ProjectId == projectId && x.ImportSnapshotId == importId && x.Status == "Pending", cancellationToken);
        var regressions = await db.BaselinePolicies.AsNoTracking().CountAsync(x => x.ProjectId == projectId && x.LastEvaluatedImportId == importId && x.Status == BaselineEvaluationStatus.Regressed, cancellationToken);
        var score = ReadinessScore(metrics);
        var status = score >= 90 && pendingApprovals == 0 && regressions == 0 ? "Ready" : score >= 75 ? "Conditional" : score >= 55 ? "At risk" : "Blocked";
        var highlights = new List<string>
        {
            $"{queue.Count(x => x.Band is "Critical" or "High")} high-priority artifacts require attention.",
            $"{metrics.GetValueOrDefault("errorCount"):0} error findings and {metrics.GetValueOrDefault("privacyOpenCount"):0} open privacy candidates remain.",
            $"{pendingApprovals} approval gates are pending and {regressions} baseline policies are regressed."
        };
        return new ExecutiveSummaryDto(importId, score, status, queue.Count, queue.Count(x => x.Band == "Critical"), pendingApprovals, regressions, metrics, highlights,
            queue.Take(5).Select(x => new ExecutivePriorityDto(x.Rank, x.ArtifactPath, x.Score, x.Band, x.Reasons.Take(3).Select(r => r.Name).ToList())).ToList(), DateTimeOffset.UtcNow);
    }

    public async Task<GeneratedExport> CreateExecutiveBriefAsync(Guid projectId, Guid importId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var project = await db.Projects.AsNoTracking().SingleAsync(x => x.Id == projectId, cancellationToken);
        var import = await db.Imports.AsNoTracking().SingleAsync(x => x.Id == importId && x.ProjectId == projectId, cancellationToken);
        var summary = await GetExecutiveSummaryAsync(projectId, importId, cancellationToken);
        var queue = await GetAdaptiveQueueAsync(projectId, importId, null, cancellationToken);
        var scenarios = await GetScenariosAsync(projectId, cancellationToken);
        var approvals = await GetApprovalGatesAsync(projectId, cancellationToken);
        var paths = storage.EnsureImportPaths(projectId, importId);
        var safeName = FileStorageService.SanitizeFileName(project.Name).Replace(' ', '-');
        var fileName = $"{safeName}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-executive-brief.zip";
        var path = Path.Combine(paths.Exports, fileName);
        await using (var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 64 * 1024, true))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
        {
            await WriteEntryAsync(archive, "executive-summary.json", summary, cancellationToken);
            await WriteEntryAsync(archive, "adaptive-queue.json", queue, cancellationToken);
            await WriteEntryAsync(archive, "scenarios.json", scenarios.Where(x => x.ImportSnapshotId == importId), cancellationToken);
            await WriteEntryAsync(archive, "approval-gates.json", approvals.Where(x => x.ImportSnapshotId == importId), cancellationToken);
            var html = BuildExecutiveHtml(project.Name, import.DisplayName, summary);
            var entry = archive.CreateEntry("executive-summary.html", CompressionLevel.Optimal);
            await using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            await writer.WriteAsync(html.AsMemory(), cancellationToken);
        }
        var info = new FileInfo(path);
        db.Exports.Add(new ExportRecordEntity { Id = Guid.NewGuid(), ProjectId = projectId, ImportSnapshotId = importId, Format = "executive", FileName = fileName, StoragePath = path, SizeBytes = info.Length, CreatedAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
        return new GeneratedExport(fileName, "application/zip", path);
    }

    private static QueuePolicyDto ToDto(QueuePolicyEntity entity) => new(entity.Id, entity.ProjectId, entity.Name, DeserializeWeightList(entity.WeightsJson), entity.SlaHours, entity.Active, entity.CreatedAtUtc, entity.UpdatedAtUtc);
    private static ScenarioRunDto ToDto(ScenarioRunEntity entity) => new(entity.Id, entity.ProjectId, entity.ImportSnapshotId, entity.Name, DeserializeAssumptions(entity.AssumptionsJson), JsonSerializer.Deserialize<ScenarioResultDto>(entity.ResultJson, JsonOptions)!, entity.CreatedAtUtc);
    private static ApprovalGateDto ToDto(ApprovalGateEntity entity) => new(entity.Id, entity.ProjectId, entity.ImportSnapshotId, entity.Name, entity.GateType, entity.RequiredRole, entity.Status, DeserializeRequirements(entity.RequirementsJson), entity.DecidedBy, entity.Rationale, entity.CreatedAtUtc, entity.DecidedAtUtc);

    private static IReadOnlyDictionary<string, double> DeserializeWeights(string json) => DeserializeWeightList(json).ToDictionary(x => x.Metric, x => x.Multiplier, StringComparer.OrdinalIgnoreCase);
    private static IReadOnlyList<QueueWeightDto> DeserializeWeightList(string json) { try { return JsonSerializer.Deserialize<List<QueueWeightDto>>(json, JsonOptions) ?? []; } catch (JsonException) { return []; } }
    private static IReadOnlyList<ScenarioAssumptionDto> DeserializeAssumptions(string json) { try { return JsonSerializer.Deserialize<List<ScenarioAssumptionDto>>(json, JsonOptions) ?? []; } catch (JsonException) { return []; } }
    private static IReadOnlyList<ApprovalRequirementDto> DeserializeRequirements(string json) { try { return JsonSerializer.Deserialize<List<ApprovalRequirementDto>>(json, JsonOptions) ?? []; } catch (JsonException) { return []; } }

    private static async Task<Dictionary<string, double>> GetSnapshotMetricsAsync(WorkbenchDbContext db, Guid projectId, Guid importId, CancellationToken cancellationToken)
    {
        var artifacts = await db.Artifacts.AsNoTracking().Where(x => x.ImportSnapshotId == importId).ToListAsync(cancellationToken);
        var findings = await db.Findings.AsNoTracking().Where(x => x.ProjectId == projectId && x.ImportSnapshotId == importId).ToListAsync(cancellationToken);
        var privacy = await db.PrivacyDetections.AsNoTracking().CountAsync(x => x.ProjectId == projectId && x.ImportSnapshotId == importId && x.Status != "Dismissed", cancellationToken);
        return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["artifactCount"] = artifacts.Count,
            ["errorCount"] = findings.Count(x => x.Severity == FindingSeverity.Error),
            ["warningCount"] = findings.Count(x => x.Severity == FindingSeverity.Warning),
            ["findingCount"] = findings.Count,
            ["parseFailureCount"] = artifacts.Count(x => x.ParseStatus == ArtifactParseStatus.Failed),
            ["unsupportedCount"] = artifacts.Count(x => x.ParseStatus == ArtifactParseStatus.Unsupported),
            ["privacyOpenCount"] = privacy
        };
    }

    private static int ReadinessScore(IReadOnlyDictionary<string, double> metrics)
    {
        var penalty = metrics.GetValueOrDefault("errorCount") * 18 + metrics.GetValueOrDefault("warningCount") * 4 + metrics.GetValueOrDefault("parseFailureCount") * 15 + metrics.GetValueOrDefault("unsupportedCount") * 2 + metrics.GetValueOrDefault("privacyOpenCount") * 8;
        return Math.Clamp((int)Math.Round(100 - penalty), 0, 100);
    }

    private static IReadOnlyList<string> BuildScenarioRecommendations(IReadOnlyDictionary<string, double> metrics)
    {
        var recommendations = new List<string>();
        if (metrics.GetValueOrDefault("errorCount") > 0) recommendations.Add("Resolve or formally reject remaining error findings.");
        if (metrics.GetValueOrDefault("privacyOpenCount") > 0) recommendations.Add("Review open privacy candidates before distribution.");
        if (metrics.GetValueOrDefault("parseFailureCount") > 0) recommendations.Add("Recover parser failures or document inventory-only acceptance.");
        if (metrics.GetValueOrDefault("unsupportedCount") > 0) recommendations.Add("Confirm unsupported formats are acceptable for the decision boundary.");
        if (recommendations.Count == 0) recommendations.Add("Complete approval gates and archive the executive brief.");
        return recommendations;
    }

    private static async Task<IReadOnlyList<ApprovalRequirementDto>> EvaluateApprovalRequirementsAsync(WorkbenchDbContext db, Guid projectId, Guid importId, CancellationToken cancellationToken)
    {
        var metrics = await GetSnapshotMetricsAsync(db, projectId, importId, cancellationToken);
        var regressed = await db.BaselinePolicies.AsNoTracking().AnyAsync(x => x.ProjectId == projectId && x.LastEvaluatedImportId == importId && x.Status == BaselineEvaluationStatus.Regressed, cancellationToken);
        return
        [
            new("No error findings", metrics["errorCount"] == 0, $"Observed {metrics["errorCount"]:0} error findings."),
            new("No unresolved privacy candidates", metrics["privacyOpenCount"] == 0, $"Observed {metrics["privacyOpenCount"]:0} open privacy candidates."),
            new("No parser failures", metrics["parseFailureCount"] == 0, $"Observed {metrics["parseFailureCount"]:0} parser failures."),
            new("Approved baseline not regressed", !regressed, regressed ? "At least one approved policy is regressed." : "No regressed approved policy was found.")
        ];
    }

    private static string BuildExecutiveHtml(string projectName, string snapshotName, ExecutiveSummaryDto summary)
    {
        var html = new StringBuilder();
        html.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>Executive brief</title>");
        html.Append("<style>body{font-family:Arial,sans-serif;margin:40px;color:#172033}h1{margin-bottom:4px}.kpis{display:grid;grid-template-columns:repeat(4,1fr);gap:12px}.card{border:1px solid #d9e1ea;border-radius:10px;padding:14px}table{width:100%;border-collapse:collapse;margin-top:20px}th,td{text-align:left;border-bottom:1px solid #e4e9ef;padding:9px}small{color:#64748b}</style></head><body>");
        html.Append($"<h1>{System.Net.WebUtility.HtmlEncode(projectName)}</h1>");
        html.Append($"<small>{System.Net.WebUtility.HtmlEncode(snapshotName)} · generated {summary.GeneratedAtUtc:u}</small>");
        html.Append($"<h2>Decision status: {summary.Status}</h2><div class=\"kpis\">");
        html.Append($"<div class=\"card\"><b>Readiness</b><div>{summary.ReadinessScore}/100</div></div>");
        html.Append($"<div class=\"card\"><b>Critical priorities</b><div>{summary.CriticalPriorities}</div></div>");
        html.Append($"<div class=\"card\"><b>Pending approvals</b><div>{summary.PendingApprovals}</div></div>");
        html.Append($"<div class=\"card\"><b>Regressions</b><div>{summary.RegressedPolicies}</div></div></div>");
        html.Append("<h2>Highlights</h2><ul>");
        foreach (var highlight in summary.Highlights) html.Append($"<li>{System.Net.WebUtility.HtmlEncode(highlight)}</li>");
        html.Append("</ul><h2>Top priorities</h2><table><tr><th>Rank</th><th>Artifact</th><th>Score</th><th>Band</th></tr>");
        foreach (var item in summary.TopPriorities)
            html.Append($"<tr><td>{item.Rank}</td><td>{System.Net.WebUtility.HtmlEncode(item.ArtifactPath)}</td><td>{item.Score}</td><td>{item.Band}</td></tr>");
        html.Append("</table></body></html>");
        return html.ToString();
    }

    private static async Task WriteEntryAsync<T>(ZipArchive archive, string name, T value, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
    }
}
