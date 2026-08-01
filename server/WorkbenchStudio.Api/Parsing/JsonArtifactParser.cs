using System.Text.Json;
using WorkbenchStudio.Api.Domain;

namespace WorkbenchStudio.Api.Parsing;

public sealed class JsonArtifactParser : IArtifactParser
{
    public string ParserId => "builtin.json";
    public string ParserVersion => "1.0.0";

    public bool CanParse(ArtifactParseContext context) =>
        context.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase);

    public async Task<ArtifactParseResult> ParseAsync(
        ArtifactParseContext context,
        CancellationToken cancellationToken)
    {
        if (context.SizeBytes > ParsingHelpers.MaximumTextParseBytes)
        {
            return new ArtifactParseResult(
                ArtifactParseStatus.ParsedWithWarnings,
                ParserId,
                ParserVersion,
                new { Kind = "json", Parsed = false, Reason = "File exceeds the JSON parse limit." },
                null,
                [new ParserFinding(
                    FindingSeverity.Warning,
                    "JSON_FILE_TOO_LARGE",
                    "JSON file exceeds parse limit",
                    $"The file is {context.SizeBytes:N0} bytes and was inventoried without loading the full JSON document.",
                    Recommendation: "Split the file or increase the parser limit after validating memory requirements.")]);
        }

        try
        {
            await using var stream = File.OpenRead(context.FilePath);
            using var document = await JsonDocument.ParseAsync(
                stream,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                    MaxDepth = 256
                },
                cancellationToken);

            var stats = new JsonStats();
            Count(document.RootElement, stats, depth: 0);
            var rootProperties = document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.EnumerateObject().Take(50).Select(x => x.Name).ToArray()
                : Array.Empty<string>();

            var preview = JsonSerializer.Serialize(
                document.RootElement,
                new JsonSerializerOptions { WriteIndented = true });
            if (preview.Length > ParsingHelpers.MaximumPreviewCharacters)
            {
                preview = preview[..ParsingHelpers.MaximumPreviewCharacters] + "\n… preview truncated …";
            }

            var findings = new List<ParserFinding>();
            if (stats.MaximumDepth > 100)
            {
                findings.Add(new ParserFinding(
                    FindingSeverity.Warning,
                    "JSON_DEEP_NESTING",
                    "Deep JSON hierarchy",
                    $"The document reaches a nesting depth of {stats.MaximumDepth}.",
                    Recommendation: "Review whether the depth is expected and whether consumers impose lower depth limits."));
            }

            return new ArtifactParseResult(
                findings.Count == 0 ? ArtifactParseStatus.Parsed : ArtifactParseStatus.ParsedWithWarnings,
                ParserId,
                ParserVersion,
                new
                {
                    Kind = "json",
                    RootType = document.RootElement.ValueKind.ToString(),
                    stats.ObjectCount,
                    stats.ArrayCount,
                    stats.PropertyCount,
                    stats.ValueCount,
                    stats.MaximumDepth,
                    RootProperties = rootProperties
                },
                preview,
                findings);
        }
        catch (JsonException exception)
        {
            return new ArtifactParseResult(
                ArtifactParseStatus.Failed,
                ParserId,
                ParserVersion,
                null,
                await ParsingHelpers.ReadTextWithLimitAsync(
                    context.FilePath,
                    ParsingHelpers.MaximumPreviewCharacters,
                    cancellationToken),
                [new ParserFinding(
                    FindingSeverity.Error,
                    "JSON_INVALID",
                    "Invalid JSON",
                    exception.Message,
                    exception.LineNumber is null ? null : $"Line {exception.LineNumber + 1}, byte {exception.BytePositionInLine + 1}",
                    Recommendation: "Correct the malformed JSON and import a new snapshot.")],
                exception.Message);
        }
    }

    private static void Count(JsonElement element, JsonStats stats, int depth)
    {
        stats.MaximumDepth = Math.Max(stats.MaximumDepth, depth);

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                stats.ObjectCount++;
                foreach (var property in element.EnumerateObject())
                {
                    stats.PropertyCount++;
                    Count(property.Value, stats, depth + 1);
                }
                break;
            case JsonValueKind.Array:
                stats.ArrayCount++;
                foreach (var item in element.EnumerateArray())
                {
                    Count(item, stats, depth + 1);
                }
                break;
            default:
                stats.ValueCount++;
                break;
        }
    }

    private sealed class JsonStats
    {
        public long ObjectCount { get; set; }
        public long ArrayCount { get; set; }
        public long PropertyCount { get; set; }
        public long ValueCount { get; set; }
        public int MaximumDepth { get; set; }
    }
}
