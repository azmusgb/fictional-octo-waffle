using System.Text;
using WorkbenchStudio.Api.Domain;

namespace WorkbenchStudio.Api.Parsing;

public sealed class CsvArtifactParser : IArtifactParser
{
    private const int MaximumAnalyzedRows = 10_000;

    public string ParserId => "builtin.csv";
    public string ParserVersion => "1.0.0";

    public bool CanParse(ArtifactParseContext context) =>
        context.Extension.Equals(".csv", StringComparison.OrdinalIgnoreCase);

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
                new { Kind = "csv", Parsed = false, Reason = "File exceeds the CSV analysis limit." },
                null,
                [new ParserFinding(
                    FindingSeverity.Warning,
                    "CSV_FILE_TOO_LARGE",
                    "CSV file exceeds analysis limit",
                    $"The file is {context.SizeBytes:N0} bytes and was inventoried without row-level analysis.")]);
        }

        try
        {
            await using var stream = File.OpenRead(context.FilePath);
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 64 * 1024);

            var rows = new List<string[]>();
            var current = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;
            var rowNumber = 1;
            var reachedEndOfStream = false;
            var findings = new List<ParserFinding>();
            var previewBuilder = new StringBuilder();

            while (rows.Count < MaximumAnalyzedRows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    reachedEndOfStream = true;
                    break;
                }

                if (previewBuilder.Length < ParsingHelpers.MaximumPreviewCharacters)
                {
                    previewBuilder.AppendLine(line);
                }

                ParseLine(line, current, field, ref inQuotes);
                if (inQuotes)
                {
                    field.Append('\n');
                    continue;
                }

                current.Add(field.ToString());
                field.Clear();
                rows.Add(current.ToArray());
                current.Clear();
                rowNumber++;
            }

            var hasAdditionalRows = false;
            if (!reachedEndOfStream && rows.Count >= MaximumAnalyzedRows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                hasAdditionalRows = await reader.ReadLineAsync(cancellationToken) is not null;
            }

            if (inQuotes)
            {
                findings.Add(new ParserFinding(
                    FindingSeverity.Error,
                    "CSV_UNCLOSED_QUOTE",
                    "Unclosed quoted field",
                    "The CSV ended while a quoted field was still open.",
                    $"Near row {rowNumber}",
                    Recommendation: "Close the quoted field and import a corrected snapshot."));
            }

            if (rows.Count == 0)
            {
                findings.Add(new ParserFinding(
                    FindingSeverity.Warning,
                    "CSV_EMPTY",
                    "Empty CSV",
                    "No rows were detected in the CSV file."));
            }

            var header = rows.Count > 0 ? rows[0] : Array.Empty<string>();
            var expectedColumns = header.Length;
            var inconsistentRows = new List<int>();
            for (var index = 1; index < rows.Count; index++)
            {
                if (rows[index].Length != expectedColumns && inconsistentRows.Count < 100)
                {
                    inconsistentRows.Add(index + 1);
                }
            }

            if (inconsistentRows.Count > 0)
            {
                findings.Add(new ParserFinding(
                    FindingSeverity.Warning,
                    "CSV_INCONSISTENT_COLUMNS",
                    "Inconsistent CSV column counts",
                    $"{inconsistentRows.Count} analyzed rows do not contain the expected {expectedColumns} columns.",
                    $"Rows: {string.Join(", ", inconsistentRows.Take(20))}",
                    Recommendation: "Review delimiters, quoting, and embedded line breaks."));
            }

            var duplicateHeaders = header
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .GroupBy(value => value.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            if (duplicateHeaders.Length > 0)
            {
                findings.Add(new ParserFinding(
                    FindingSeverity.Warning,
                    "CSV_DUPLICATE_HEADERS",
                    "Duplicate CSV headers",
                    $"Duplicate column names were found: {string.Join(", ", duplicateHeaders)}.",
                    "Header row",
                    Recommendation: "Use unique column names to make downstream mapping deterministic."));
            }

            var analyzedDataRows = Math.Max(0, rows.Count - 1);
            var nullCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var column = 0; column < header.Length; column++)
            {
                var name = string.IsNullOrWhiteSpace(header[column]) ? $"Column {column + 1}" : header[column];
                nullCounts[name] = rows.Skip(1).Count(row => column >= row.Length || string.IsNullOrWhiteSpace(row[column]));
            }

            if (hasAdditionalRows)
            {
                findings.Add(new ParserFinding(
                    FindingSeverity.Info,
                    "CSV_ANALYSIS_TRUNCATED",
                    "CSV analysis sample limit reached",
                    $"Analysis was limited to the first {MaximumAnalyzedRows:N0} rows."));
            }

            var status = findings.Any(x => x.Severity == FindingSeverity.Error)
                ? ArtifactParseStatus.Failed
                : findings.Count > 0
                    ? ArtifactParseStatus.ParsedWithWarnings
                    : ArtifactParseStatus.Parsed;

            var preview = previewBuilder.ToString();
            if (preview.Length > ParsingHelpers.MaximumPreviewCharacters)
            {
                preview = preview[..ParsingHelpers.MaximumPreviewCharacters] + "\n… preview truncated …";
            }

            return new ArtifactParseResult(
                status,
                ParserId,
                ParserVersion,
                new
                {
                    Kind = "csv",
                    ColumnCount = expectedColumns,
                    AnalyzedRowCount = rows.Count,
                    DataRowCount = analyzedDataRows,
                    Header = header,
                    EmptyValueCounts = nullCounts,
                    InconsistentRows = inconsistentRows
                },
                preview,
                findings,
                status == ArtifactParseStatus.Failed ? "CSV structural validation failed." : null);
        }
        catch (DecoderFallbackException exception)
        {
            return new ArtifactParseResult(
                ArtifactParseStatus.Failed,
                ParserId,
                ParserVersion,
                null,
                null,
                [new ParserFinding(
                    FindingSeverity.Error,
                    "CSV_ENCODING_ERROR",
                    "CSV encoding could not be decoded",
                    exception.Message,
                    Recommendation: "Save the file as UTF-8 or UTF-8 with BOM and import it again.")],
                exception.Message);
        }
    }

    private static void ParseLine(
        string line,
        List<string> current,
        StringBuilder field,
        ref bool inQuotes)
    {
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                current.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(character);
            }
        }
    }
}