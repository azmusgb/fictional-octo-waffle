using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkbenchStudio.Api.Data;
using WorkbenchStudio.Api.Domain;

namespace WorkbenchStudio.Api.Services;

public sealed class DecisionOperationsService(
    IDbContextFactory<WorkbenchDbContext> dbContextFactory,
    FileStorageService storage,
    DataProfileService profileService,
    LineageService lineageService,
    PrivacyService privacyService)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "for", "from", "how", "in", "is", "it", "of", "on", "or", "that", "the", "this", "to", "was", "were", "what", "when", "where", "which", "who", "why", "with"
    };

    public async Task<IReadOnlyList<TriageItemDto>> GetTriageAsync(Guid projectId, Guid importId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var artifacts = await db.Artifacts.AsNoTracking()
            .Where(x => x.ImportSnapshotId == importId)
            .OrderBy(x => x.RelativePath)
            .ToListAsync(cancellationToken);
        var findings = await db.Findings.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.ImportSnapshotId == importId && x.ArtifactId != null)
            .ToListAsync(cancellationToken);
        var artifactIds = artifacts.Select(a => a.Id).ToArray();
        var reviews = await db.ArtifactReviews.AsNoTracking()
            .Where(x => artifactIds.Contains(x.ArtifactId))
            .ToDictionaryAsync(x => x.ArtifactId, cancellationToken);
        var privacy = await db.PrivacyDetections.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.ImportSnapshotId == importId && x.Status != "Dismissed")
            .ToListAsync(cancellationToken);
        var lineage = await db.LineageEdges.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.ImportSnapshotId == importId)
            .ToListAsync(cancellationToken);

        var findingsByArtifact = findings.GroupBy(x => x.ArtifactId!.Value).ToDictionary(x => x.Key, x => x.ToList());
        var privacyByArtifact = privacy.GroupBy(x => x.ArtifactId).ToDictionary(x => x.Key, x => x.Count());
        var impactByArtifact = lineage
            .SelectMany(x => x.ToArtifactId.HasValue ? new[] { x.FromArtifactId, x.ToArtifactId.Value } : new[] { x.FromArtifactId })
            .GroupBy(x => x)
            .ToDictionary(x => x.Key, x => x.Count());

        return artifacts.Select(artifact =>
        {
            var factors = new List<TriageFactorDto>();
            var artifactFindings = findingsByArtifact.GetValueOrDefault(artifact.Id) ?? [];
            var errorCount = artifactFindings.Count(x => x.Severity == FindingSeverity.Error);
            var warningCount = artifactFindings.Count(x => x.Severity == FindingSeverity.Warning);
            var infoCount = artifactFindings.Count(x => x.Severity == FindingSeverity.Info);
            AddFactor(factors, "Error findings", errorCount * 30, $"{errorCount} error-level findings");
            AddFactor(factors, "Warning findings", warningCount * 14, $"{warningCount} warning-level findings");
            AddFactor(factors, "Informational findings", infoCount * 4, $"{infoCount} informational findings");

            var privacyCount = privacyByArtifact.GetValueOrDefault(artifact.Id);
            AddFactor(factors, "Sensitive-value candidates", privacyCount * 18, $"{privacyCount} open or confirmed privacy candidates");

            var impactCount = impactByArtifact.GetValueOrDefault(artifact.Id);
            AddFactor(factors, "Lineage impact", Math.Min(20, impactCount * 3), $"{impactCount} connected impact edges");

            if (artifact.ParseStatus == ArtifactParseStatus.Failed) AddFactor(factors, "Parser failure", 25, "Parser could not produce structured evidence");
            if (artifact.ParseStatus == ArtifactParseStatus.Unsupported) AddFactor(factors, "Unsupported format", 8, "Artifact remains inventory-only");

            var reviewStatus = reviews.TryGetValue(artifact.Id, out var review) ? review.Status : ArtifactReviewStatus.Unreviewed;
            var reviewPoints = reviewStatus switch
            {
                ArtifactReviewStatus.NeedsAttention => 20,
                ArtifactReviewStatus.Unreviewed => 12,
                ArtifactReviewStatus.InReview => 5,
                _ => 0
            };
            AddFactor(factors, "Review state", reviewPoints, reviewStatus.ToString());

            var score = Math.Min(100, factors.Sum(x => x.Points));
            var band = score switch { >= 70 => "Critical", >= 45 => "High", >= 20 => "Medium", _ => "Low" };
            return new TriageItemDto(artifact.Id, artifact.RelativePath, score, band, reviewStatus.ToString(), artifactFindings.Count, impactCount, privacyCount, factors);
        }).OrderByDescending(x => x.PriorityScore).ThenBy(x => x.ArtifactPath).Take(100).ToList();
    }

    public async Task<BaselinePolicyDto> CreateBaselineAsync(Guid projectId, CreateBaselinePolicyRequest request, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var baselineExists = await db.Imports.AnyAsync(x => x.Id == request.BaselineImportId && x.ProjectId == projectId, cancellationToken);
        if (!baselineExists) throw new KeyNotFoundException("Baseline snapshot was not found.");
        var rules = request.Rules is { Count: > 0 } ? request.Rules : await CreateDefaultRulesAsync(db, projectId, request.BaselineImportId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var entity = new BaselinePolicyEntity
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Name = string.IsNullOrWhiteSpace(request.Name) ? "Approved snapshot baseline" : request.Name.Trim(),
            BaselineImportId = request.BaselineImportId, RulesJson = JsonSerializer.Serialize(rules, JsonOptions), CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.BaselinePolicies.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<BaselineEvaluationDto> EvaluateBaselineAsync(Guid projectId, Guid policyId, Guid currentImportId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var policy = await db.BaselinePolicies.SingleOrDefaultAsync(x => x.Id == policyId && x.ProjectId == projectId, cancellationToken)
            ?? throw new KeyNotFoundException("Baseline policy was not found.");
        if (!await db.Imports.AnyAsync(x => x.Id == currentImportId && x.ProjectId == projectId, cancellationToken))
            throw new KeyNotFoundException("Current snapshot was not found.");

        var rules = DeserializeRules(policy.RulesJson);
        var metrics = await GetSnapshotMetricsAsync(db, projectId, currentImportId, cancellationToken);
        var results = rules.Select(rule => EvaluateRule(rule, metrics.GetValueOrDefault(rule.Metric))).ToList();
        var failed = results.Count(x => !x.Passed);
        var hasErrorFailure = results.Any(x => !x.Passed && x.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase));
        var improved = results.Any(x => x.Passed && x.Operator == "<=" && x.Actual < x.Expected);
        var status = failed == 0 ? (improved ? BaselineEvaluationStatus.Improved : BaselineEvaluationStatus.Passed)
            : hasErrorFailure ? BaselineEvaluationStatus.Regressed : BaselineEvaluationStatus.NeedsApproval;
        var evaluatedAt = DateTimeOffset.UtcNow;
        var response = new BaselineEvaluationDto(policy.Id, policy.BaselineImportId, currentImportId, status.ToString(), results.Count - failed, failed, results, evaluatedAt);
        policy.Status = status;
        policy.LastEvaluatedImportId = currentImportId;
        policy.LastResultJson = JsonSerializer.Serialize(response, JsonOptions);
        policy.LastEvaluatedAtUtc = evaluatedAt;
        policy.UpdatedAtUtc = evaluatedAt;
        await db.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<IReadOnlyList<BaselinePolicyDto>> GetBaselinesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.BaselinePolicies.AsNoTracking().Where(x => x.ProjectId == projectId).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<AutomationRecipeDto>> GetRecipesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.AutomationRecipes.AsNoTracking().Where(x => x.ProjectId == projectId).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task<AutomationRecipeDto> CreateRecipeAsync(Guid projectId, CreateAutomationRecipeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Steps.Count == 0) throw new ArgumentException("A recipe name and at least one step are required.");
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Projects.AnyAsync(x => x.Id == projectId, cancellationToken)) throw new KeyNotFoundException("Project was not found.");
        var now = DateTimeOffset.UtcNow;
        var entity = new AutomationRecipeEntity
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Name = request.Name.Trim(), Description = request.Description.Trim(),
            StepsJson = JsonSerializer.Serialize(request.Steps, JsonOptions), TriggerMode = NormalizeTrigger(request.TriggerMode),
            ScheduleIntervalMinutes = Math.Clamp(request.ScheduleIntervalMinutes ?? 1440, 60, 10080), CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.AutomationRecipes.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<AutomationRecipeDto> UpdateRecipeAsync(Guid projectId, Guid recipeId, UpdateAutomationRecipeRequest request, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.AutomationRecipes.SingleOrDefaultAsync(x => x.Id == recipeId && x.ProjectId == projectId, cancellationToken)
            ?? throw new KeyNotFoundException("Automation recipe was not found.");
        if (request.Enabled.HasValue) entity.Enabled = request.Enabled.Value;
        if (!string.IsNullOrWhiteSpace(request.TriggerMode)) entity.TriggerMode = NormalizeTrigger(request.TriggerMode);
        if (request.ScheduleIntervalMinutes.HasValue) entity.ScheduleIntervalMinutes = Math.Clamp(request.ScheduleIntervalMinutes.Value, 60, 10080);
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<AutomationRecipeDto> RunRecipeAsync(Guid projectId, Guid recipeId, Guid importId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var recipe = await db.AutomationRecipes.SingleOrDefaultAsync(x => x.Id == recipeId && x.ProjectId == projectId, cancellationToken)
            ?? throw new KeyNotFoundException("Automation recipe was not found.");
        var steps = DeserializeSteps(recipe.StepsJson);
        recipe.Status = AutomationRecipeStatus.Running;
        recipe.ProgressPercent = 0;
        await db.SaveChangesAsync(cancellationToken);
        var summaries = new List<string>();
        var warnings = 0;
        try
        {
            for (var index = 0; index < steps.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var step = steps[index];
                try
                {
                    switch (step.Type.Trim().ToLowerInvariant())
                    {
                        case "profile": summaries.Add($"Profiled {await profileService.ProfileImportAsync(projectId, importId, cancellationToken)} artifacts"); break;
                        case "privacy": summaries.Add($"Detected {await privacyService.ScanAsync(projectId, importId, cancellationToken)} privacy candidates"); break;
                        case "impact": summaries.Add($"Built {await lineageService.RebuildAsync(projectId, importId, cancellationToken)} lineage edges"); break;
                        case "baseline":
                            var policy = await db.BaselinePolicies.AsNoTracking().Where(x => x.ProjectId == projectId).OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
                            if (policy is null) { warnings++; summaries.Add("Skipped baseline evaluation because no policy exists"); }
                            else { var evaluation = await EvaluateBaselineAsync(projectId, policy.Id, importId, cancellationToken); summaries.Add($"Baseline result: {evaluation.Status}"); }
                            break;
                        case "triage": summaries.Add($"Ranked {(await GetTriageAsync(projectId, importId, cancellationToken)).Count} artifacts for review"); break;
                        default: warnings++; summaries.Add($"Skipped unsupported step: {step.Name}"); break;
                    }
                }
                catch (Exception) when (!step.Required && !cancellationToken.IsCancellationRequested) { warnings++; summaries.Add($"Optional step failed: {step.Name}"); }
                recipe.ProgressPercent = (int)Math.Round((index + 1) * 100d / steps.Count);
                await db.SaveChangesAsync(cancellationToken);
            }
            recipe.Status = warnings > 0 ? AutomationRecipeStatus.CompletedWithWarnings : AutomationRecipeStatus.Completed;
        }
        catch (Exception exception)
        {
            recipe.Status = AutomationRecipeStatus.Failed;
            summaries.Add(exception.Message);
        }
        recipe.LastRunAtUtc = DateTimeOffset.UtcNow;
        recipe.LastRunSummary = string.Join(" · ", summaries);
        recipe.UpdatedAtUtc = recipe.LastRunAtUtc.Value;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(recipe);
    }

    public async Task<EvidenceAnswerDto> AskEvidenceAsync(Guid projectId, Guid importId, EvidenceQuestionRequest request, CancellationToken cancellationToken)
    {
        var question = request.Question?.Trim() ?? string.Empty;
        if (question.Length < 3) throw new ArgumentException("Ask a concrete evidence question.");
        var terms = Tokenize(question).ToArray();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var artifacts = await db.Artifacts.AsNoTracking().Where(x => x.ImportSnapshotId == importId).ToListAsync(cancellationToken);
        var findings = await db.Findings.AsNoTracking().Where(x => x.ProjectId == projectId && x.ImportSnapshotId == importId).ToListAsync(cancellationToken);
        var artifactPaths = artifacts.ToDictionary(x => x.Id, x => x.RelativePath);
        var candidates = new List<(int Score, EvidenceCitationDto Citation)>();
        foreach (var finding in findings)
        {
            var text = $"{finding.Title} {finding.Message} {finding.RuleId} {finding.SourceLocation} {finding.EvidenceExcerpt}";
            var score = Score(text, terms) + (finding.Severity == FindingSeverity.Error ? 4 : finding.Severity == FindingSeverity.Warning ? 2 : 0);
            if (score > 0)
            {
                candidates.Add((score, new EvidenceCitationDto(finding.ArtifactId, finding.Id,
                    finding.ArtifactId.HasValue ? artifactPaths.GetValueOrDefault(finding.ArtifactId.Value, "Project") : "Project",
                    finding.SourceLocation, finding.EvidenceExcerpt ?? finding.Message, "Validation finding")));
            }
        }
        foreach (var artifact in artifacts)
        {
            var text = $"{artifact.RelativePath} {artifact.Name} {artifact.Extension} {artifact.ParserId} {artifact.PreviewText}";
            var score = Score(text, terms);
            if (score > 0)
            {
                var excerpt = string.IsNullOrWhiteSpace(artifact.PreviewText) ? $"Artifact {artifact.RelativePath} ({artifact.MediaType})" : TrimExcerpt(artifact.PreviewText, 360);
                candidates.Add((score, new EvidenceCitationDto(artifact.Id, null, artifact.RelativePath, null, excerpt, "Artifact content and metadata")));
            }
        }
        var maximum = Math.Clamp(request.MaximumCitations ?? 6, 1, 12);
        var citations = candidates.OrderByDescending(x => x.Score).ThenBy(x => x.Citation.ArtifactPath).Select(x => x.Citation)
            .DistinctBy(x => new { x.ArtifactId, x.FindingId, x.SourceLocation }).Take(maximum).ToList();
        var confidence = citations.Count switch { >= 4 => "High", >= 2 => "Moderate", 1 => "Low", _ => "Insufficient evidence" };
        var answer = citations.Count == 0
            ? "No directly supporting evidence was found in the selected immutable snapshot. Refine the question or inspect another snapshot."
            : $"Found {citations.Count} source-grounded evidence reference{(citations.Count == 1 ? string.Empty : "s")}. The strongest match is {citations[0].ArtifactPath}{(string.IsNullOrWhiteSpace(citations[0].SourceLocation) ? string.Empty : $" at {citations[0].SourceLocation}")}. Review the citations before treating the result as a conclusion.";
        var followUps = terms.Take(3).Select(term => $"Show findings related to {term}").Append("Compare the cited artifacts with the previous snapshot").Distinct().Take(4).ToList();
        return new EvidenceAnswerDto(answer, confidence, citations, followUps);
    }

    public async Task<GeneratedExport> CreateDecisionBriefAsync(Guid projectId, Guid importId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var project = await db.Projects.AsNoTracking().SingleAsync(x => x.Id == projectId, cancellationToken);
        var import = await db.Imports.AsNoTracking().SingleAsync(x => x.Id == importId && x.ProjectId == projectId, cancellationToken);
        var findings = await db.Findings.AsNoTracking().Where(x => x.ProjectId == projectId && x.ImportSnapshotId == importId).ToListAsync(cancellationToken);
        var baselines = await db.BaselinePolicies.AsNoTracking().Where(x => x.ProjectId == projectId).ToListAsync(cancellationToken);
        var profiles = await db.DataProfiles.AsNoTracking().Where(x => x.ProjectId == projectId && x.ImportSnapshotId == importId).ToListAsync(cancellationToken);
        var triage = await GetTriageAsync(projectId, importId, cancellationToken);
        var paths = storage.EnsureImportPaths(projectId, importId);
        var safeName = FileStorageService.SanitizeFileName(project.Name).Replace(' ', '-');
        var fileName = $"{safeName}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-decision-brief.zip";
        var path = Path.Combine(paths.Exports, fileName);
        await using (var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 64 * 1024, true))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
        {
            await WriteEntryAsync(archive, "manifest.json", new { generatedAtUtc = DateTimeOffset.UtcNow, project = new { project.Id, project.Name }, snapshot = DtoMapper.ToDto(import), purpose = "Portable Workbench Studio decision handoff" }, cancellationToken);
            await WriteEntryAsync(archive, "triage.json", triage, cancellationToken);
            await WriteEntryAsync(archive, "findings.json", findings.Select(x => DtoMapper.ToDto(x)), cancellationToken);
            await WriteEntryAsync(archive, "baselines.json", baselines.Select(ToDto), cancellationToken);
            await WriteEntryAsync(archive, "profiles.json", profiles.Select(x => new { x.ArtifactId, x.ProfileType, metrics = JsonSerializer.Deserialize<JsonElement>(x.MetricsJson), issues = JsonSerializer.Deserialize<JsonElement>(x.IssuesJson), x.CreatedAtUtc }), cancellationToken);
            var readme = "Workbench Studio decision brief\n\nThis package contains source-linked decision data for one immutable snapshot. Review citations and original evidence before relying on derived conclusions.\n";
            var entry = archive.CreateEntry("README.txt", CompressionLevel.Optimal);
            await using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            await writer.WriteAsync(readme.AsMemory(), cancellationToken);
        }
        var info = new FileInfo(path);
        db.Exports.Add(new ExportRecordEntity { Id = Guid.NewGuid(), ProjectId = projectId, ImportSnapshotId = importId, Format = "brief", FileName = fileName, StoragePath = path, SizeBytes = info.Length, CreatedAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
        return new GeneratedExport(fileName, "application/zip", path);
    }

    public static BaselinePolicyDto ToDto(BaselinePolicyEntity entity)
    {
        JsonElement? result = null;
        if (!string.IsNullOrWhiteSpace(entity.LastResultJson)) result = JsonSerializer.Deserialize<JsonElement>(entity.LastResultJson);
        return new BaselinePolicyDto(entity.Id, entity.ProjectId, entity.Name, entity.BaselineImportId, DeserializeRules(entity.RulesJson), entity.Status.ToString(), entity.LastEvaluatedImportId, result, entity.LastEvaluatedAtUtc);
    }

    public static AutomationRecipeDto ToDto(AutomationRecipeEntity entity) =>
        new(entity.Id, entity.ProjectId, entity.Name, entity.Description, DeserializeSteps(entity.StepsJson), entity.Enabled, entity.TriggerMode,
            entity.ScheduleIntervalMinutes, entity.Status.ToString(), entity.ProgressPercent, entity.LastRunSummary, entity.LastRunAtUtc);

    private static async Task<IReadOnlyList<BaselineRuleDto>> CreateDefaultRulesAsync(WorkbenchDbContext db, Guid projectId, Guid importId, CancellationToken cancellationToken)
    {
        var metrics = await GetSnapshotMetricsAsync(db, projectId, importId, cancellationToken);
        return
        [
            new("errorCount", "<=", metrics["errorCount"], "Error"),
            new("warningCount", "<=", metrics["warningCount"], "Warning"),
            new("parseFailureCount", "<=", metrics["parseFailureCount"], "Error"),
            new("unsupportedCount", "<=", metrics["unsupportedCount"], "Warning"),
            new("artifactCount", ">=", metrics["artifactCount"], "Warning")
        ];
    }

    private static async Task<Dictionary<string, double>> GetSnapshotMetricsAsync(WorkbenchDbContext db, Guid projectId, Guid importId, CancellationToken cancellationToken)
    {
        var artifacts = await db.Artifacts.AsNoTracking().Where(x => x.ImportSnapshotId == importId).ToListAsync(cancellationToken);
        var findings = await db.Findings.AsNoTracking().Where(x => x.ProjectId == projectId && x.ImportSnapshotId == importId).ToListAsync(cancellationToken);
        var privacyOpen = await db.PrivacyDetections.AsNoTracking().CountAsync(x => x.ProjectId == projectId && x.ImportSnapshotId == importId && x.Status != "Dismissed", cancellationToken);
        return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["artifactCount"] = artifacts.Count,
            ["totalBytes"] = artifacts.Sum(x => (double)x.SizeBytes),
            ["findingCount"] = findings.Count,
            ["errorCount"] = findings.Count(x => x.Severity == FindingSeverity.Error),
            ["warningCount"] = findings.Count(x => x.Severity == FindingSeverity.Warning),
            ["parseFailureCount"] = artifacts.Count(x => x.ParseStatus == ArtifactParseStatus.Failed),
            ["unsupportedCount"] = artifacts.Count(x => x.ParseStatus == ArtifactParseStatus.Unsupported),
            ["privacyOpenCount"] = privacyOpen
        };
    }

    private static BaselineRuleResultDto EvaluateRule(BaselineRuleDto rule, double actual)
    {
        var passed = rule.Operator.Trim() switch
        {
            "<=" => actual <= rule.Value,
            ">=" => actual >= rule.Value,
            "==" => Math.Abs(actual - rule.Value) < 0.0001,
            _ => false
        };
        var message = $"{rule.Metric} {actual.ToString("0.##", CultureInfo.InvariantCulture)} {(passed ? "meets" : "violates")} policy {rule.Operator} {rule.Value.ToString("0.##", CultureInfo.InvariantCulture)}.";
        return new BaselineRuleResultDto(rule.Metric, rule.Operator, rule.Value, actual, passed, rule.Severity, message);
    }

    private static IReadOnlyList<BaselineRuleDto> DeserializeRules(string json)
    {
        try { return JsonSerializer.Deserialize<List<BaselineRuleDto>>(json, JsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }

    private static IReadOnlyList<AutomationStepDto> DeserializeSteps(string json)
    {
        try { return JsonSerializer.Deserialize<List<AutomationStepDto>>(json, JsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }

    private static string NormalizeTrigger(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "hourly" => "Hourly",
        "daily" => "Daily",
        "onsnapshot" or "on snapshot" => "OnSnapshot",
        _ => "Manual"
    };

    private static void AddFactor(ICollection<TriageFactorDto> factors, string name, int points, string explanation)
    {
        if (points > 0) factors.Add(new TriageFactorDto(name, points, explanation));
    }

    private static IEnumerable<string> Tokenize(string value) => value.Split([' ', '\t', '\r', '\n', '.', ',', ':', ';', '/', '\\', '(', ')', '[', ']', '{', '}', '"', '\''], StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length >= 2 && !StopWords.Contains(x)).Distinct();

    private static int Score(string? text, IReadOnlyList<string> terms)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var normalized = text.ToLowerInvariant();
        return terms.Sum(term => normalized.Contains(term, StringComparison.Ordinal) ? 3 : 0);
    }

    private static string TrimExcerpt(string value, int maximum) => value.Length <= maximum ? value : value[..maximum] + "…";

    private static async Task WriteEntryAsync<T>(ZipArchive archive, string name, T value, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
    }
}
