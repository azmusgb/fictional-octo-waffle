using System.Text;
using System.Text.RegularExpressions;
using WorkbenchStudio.Api.Domain;

namespace WorkbenchStudio.Api.Parsing;

public sealed partial class TextLogArtifactParser : IArtifactParser
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".log", ".txt", ".out", ".trace"
    };

    public string ParserId => "builtin.text-log";
    public string ParserVersion => "1.0.0";

    public bool CanParse(ArtifactParseContext context) =>
        SupportedExtensions.Contains(context.Extension);

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
                new { Kind = "text", Parsed = false, Reason = "File exceeds the text analysis limit." },
                null,
                [new ParserFinding(
                    FindingSeverity.Warning,
                    "TEXT_FILE_TOO_LARGE",
                    "Text file exceeds analysis limit",
                    $"The file is {context.SizeBytes:N0} bytes and was inventoried without full line analysis.")]);
        }

        var lineCount = 0;
        var emptyLineCount = 0;
        var errorCount = 0;
        var warningCount = 0;
        var exceptionCount = 0;
        var findings = new List<ParserFinding>();
        var preview = new StringBuilder();

        await using var stream = File.OpenRead(context.FilePath);
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 64 * 1024);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineCount++;

            if (preview.Length < ParsingHelpers.MaximumPreviewCharacters)
            {
                preview.AppendLine(line);
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                emptyLineCount++;
                continue;
            }

            if (ErrorPattern().IsMatch(line))
            {
                errorCount++;
                if (findings.Count(x => x.RuleId == "LOG_ERROR_ENTRY") < 25)
                {
                    findings.Add(new ParserFinding(
                        FindingSeverity.Error,
                        "LOG_ERROR_ENTRY",
                        "Error entry in log",
                        "The log contains an error-level entry.",
                        $"Line {lineCount}",
                        ParsingHelpers.TrimEvidence(line),
                        "Review the surrounding log context and originating component."));
                }
            }
            else if (WarningPattern().IsMatch(line))
            {
                warningCount++;
                if (findings.Count(x => x.RuleId == "LOG_WARNING_ENTRY") < 25)
                {
                    findings.Add(new ParserFinding(
                        FindingSeverity.Warning,
                        "LOG_WARNING_ENTRY",
                        "Warning entry in log",
                        "The log contains a warning-level entry.",
                        $"Line {lineCount}",
                        ParsingHelpers.TrimEvidence(line)));
                }
            }

            if (ExceptionPattern().IsMatch(line))
            {
                exceptionCount++;
            }
        }

        if (lineCount == 0)
        {
            findings.Add(new ParserFinding(
                FindingSeverity.Info,
                "TEXT_EMPTY",
                "Empty text file",
                "No lines were detected in the text file."));
        }

        var status = findings.Any(x => x.Severity == FindingSeverity.Error)
            ? ArtifactParseStatus.ParsedWithWarnings
            : findings.Count > 0
                ? ArtifactParseStatus.ParsedWithWarnings
                : ArtifactParseStatus.Parsed;

        var previewText = preview.ToString();
        if (previewText.Length > ParsingHelpers.MaximumPreviewCharacters)
        {
            previewText = previewText[..ParsingHelpers.MaximumPreviewCharacters] + "\n… preview truncated …";
        }

        return new ArtifactParseResult(
            status,
            ParserId,
            ParserVersion,
            new
            {
                Kind = "text-log",
                LineCount = lineCount,
                EmptyLineCount = emptyLineCount,
                ErrorEntryCount = errorCount,
                WarningEntryCount = warningCount,
                ExceptionReferenceCount = exceptionCount
            },
            previewText,
            findings);
    }

    [GeneratedRegex(@"(^|\s|\[)(error|fatal|critical)(\s|:|\]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ErrorPattern();

    [GeneratedRegex(@"(^|\s|\[)(warn|warning)(\s|:|\]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WarningPattern();

    [GeneratedRegex(@"\b(exception|stack trace|traceback)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExceptionPattern();
}