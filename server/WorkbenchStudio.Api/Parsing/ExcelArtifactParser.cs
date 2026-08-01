using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using WorkbenchStudio.Api.Domain;

namespace WorkbenchStudio.Api.Parsing;

public sealed class ExcelArtifactParser : IArtifactParser
{
    private const int MaximumEntries = 5_000;
    private const long MaximumExpandedXmlBytes = 64L * 1024 * 1024;
    private const int MaximumPreviewRows = 25;
    private const int MaximumPreviewColumns = 20;

    private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

    public string ParserId => "builtin.xlsx";
    public string ParserVersion => "2.0.0";

    public bool CanParse(ArtifactParseContext context) =>
        context.Extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase);

    public Task<ArtifactParseResult> ParseAsync(
        ArtifactParseContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var archive = ZipFile.OpenRead(context.FilePath);
            ValidateArchive(archive);

            var workbookEntry = archive.GetEntry("xl/workbook.xml");
            var relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
            if (workbookEntry is null || relationshipsEntry is null)
            {
                return Task.FromResult(Failed(
                    "XLSX_WORKBOOK_MISSING",
                    "Workbook metadata is missing",
                    "The XLSX package does not contain the workbook metadata required to resolve worksheets."));
            }

            var workbook = LoadXml(workbookEntry);
            var relationships = LoadXml(relationshipsEntry);
            var sharedStrings = ReadSharedStrings(archive.GetEntry("xl/sharedStrings.xml"));
            var relationshipTargets = relationships
                .Descendants(PackageRelationshipNamespace + "Relationship")
                .Where(element => element.Attribute("Id") is not null && element.Attribute("Target") is not null)
                .ToDictionary(
                    element => element.Attribute("Id")!.Value,
                    element => NormalizeWorksheetTarget(element.Attribute("Target")!.Value),
                    StringComparer.Ordinal);

            var findings = new List<ParserFinding>();
            var sheetSummaries = new List<object>();
            var preview = new StringBuilder();
            var totalCells = 0;
            var totalFormulas = 0;
            var hiddenSheetCount = 0;

            var sheets = workbook
                .Descendants(SpreadsheetNamespace + "sheet")
                .ToArray();

            foreach (var sheet in sheets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = sheet.Attribute("name")?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = "Unnamed sheet";
                }

                var state = sheet.Attribute("state")?.Value ?? "visible";
                if (!state.Equals("visible", StringComparison.OrdinalIgnoreCase))
                {
                    hiddenSheetCount++;
                }

                var relationshipId = sheet.Attribute(RelationshipNamespace + "id")?.Value;
                if (string.IsNullOrWhiteSpace(relationshipId) || !relationshipTargets.TryGetValue(relationshipId, out var target))
                {
                    findings.Add(new ParserFinding(
                        FindingSeverity.Warning,
                        "XLSX_SHEET_TARGET_MISSING",
                        "Worksheet target could not be resolved",
                        $"The worksheet '{name}' references a relationship that is missing from the workbook package.",
                        name));
                    continue;
                }

                var worksheetEntry = archive.GetEntry(target);
                if (worksheetEntry is null)
                {
                    findings.Add(new ParserFinding(
                        FindingSeverity.Warning,
                        "XLSX_SHEET_FILE_MISSING",
                        "Worksheet file is missing",
                        $"The worksheet '{name}' resolves to '{target}', but that file is not present in the XLSX package.",
                        name));
                    continue;
                }

                var summary = AnalyzeWorksheet(name, state, worksheetEntry, sharedStrings, cancellationToken);
                totalCells += summary.NonEmptyCellCount;
                totalFormulas += summary.FormulaCount;
                sheetSummaries.Add(new
                {
                    summary.Name,
                    summary.State,
                    summary.Dimension,
                    summary.RowCount,
                    summary.ColumnCount,
                    summary.NonEmptyCellCount,
                    summary.FormulaCount,
                    summary.ErrorCellCount,
                    summary.MergedCellCount
                });

                if (preview.Length < ParsingHelpers.MaximumPreviewCharacters)
                {
                    preview.AppendLine($"## {summary.Name} ({summary.State})");
                    preview.Append(summary.Preview);
                    preview.AppendLine();
                }
            }

            if (sheets.Length == 0)
            {
                findings.Add(new ParserFinding(
                    FindingSeverity.Warning,
                    "XLSX_NO_SHEETS",
                    "Workbook contains no worksheets",
                    "No worksheet definitions were found in the workbook metadata."));
            }

            if (hiddenSheetCount > 0)
            {
                findings.Add(new ParserFinding(
                    FindingSeverity.Info,
                    "XLSX_HIDDEN_SHEETS",
                    "Workbook contains hidden worksheets",
                    $"{hiddenSheetCount:N0} worksheet(s) are hidden or very hidden.",
                    Recommendation: "Review hidden worksheets when validating workbook completeness."));
            }

            if (totalFormulas > 0)
            {
                findings.Add(new ParserFinding(
                    FindingSeverity.Info,
                    "XLSX_FORMULAS_PRESENT",
                    "Workbook contains formulas",
                    $"{totalFormulas:N0} formula cell(s) were detected. Cached values are inventoried, but formulas are not recalculated.",
                    Recommendation: "Open the workbook in a spreadsheet engine when recalculation or formula semantics must be verified."));
            }

            var previewText = preview.ToString();
            if (previewText.Length > ParsingHelpers.MaximumPreviewCharacters)
            {
                previewText = previewText[..ParsingHelpers.MaximumPreviewCharacters] + "\n… preview truncated …";
            }

            var status = findings.Any(item => item.Severity == FindingSeverity.Error)
                ? ArtifactParseStatus.Failed
                : findings.Count > 0
                    ? ArtifactParseStatus.ParsedWithWarnings
                    : ArtifactParseStatus.Parsed;

            return Task.FromResult(new ArtifactParseResult(
                status,
                ParserId,
                ParserVersion,
                new
                {
                    Kind = "excel-workbook",
                    WorkbookFormat = "Office Open XML",
                    SheetCount = sheets.Length,
                    HiddenSheetCount = hiddenSheetCount,
                    SharedStringCount = sharedStrings.Count,
                    NonEmptyCellCount = totalCells,
                    FormulaCount = totalFormulas,
                    Sheets = sheetSummaries
                },
                previewText,
                findings));
        }
        catch (InvalidDataException exception)
        {
            return Task.FromResult(Failed(
                "XLSX_INVALID_PACKAGE",
                "Invalid or unsafe XLSX package",
                exception.Message));
        }
        catch (XmlException exception)
        {
            return Task.FromResult(Failed(
                "XLSX_INVALID_XML",
                "Workbook XML is invalid",
                exception.Message));
        }
    }

    private static ArtifactParseResult Failed(string ruleId, string title, string message) =>
        new(
            ArtifactParseStatus.Failed,
            "builtin.xlsx",
            "2.0.0",
            null,
            null,
            [new ParserFinding(
                FindingSeverity.Error,
                ruleId,
                title,
                message,
                Recommendation: "Retain the original workbook, repair it in a trusted spreadsheet application, and import a new snapshot.")],
            message);

    private static void ValidateArchive(ZipArchive archive)
    {
        if (archive.Entries.Count > MaximumEntries)
        {
            throw new InvalidDataException($"The workbook contains more than {MaximumEntries:N0} package entries.");
        }

        var expandedBytes = 0L;
        foreach (var entry in archive.Entries)
        {
            expandedBytes = checked(expandedBytes + entry.Length);
            if (expandedBytes > MaximumExpandedXmlBytes)
            {
                throw new InvalidDataException($"The workbook expands beyond the {MaximumExpandedXmlBytes:N0}-byte analysis limit.");
            }
        }
    }

    private static XDocument LoadXml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            IgnoreComments = false,
            IgnoreWhitespace = false
        });
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchiveEntry? entry)
    {
        if (entry is null)
        {
            return Array.Empty<string>();
        }

        var document = LoadXml(entry);
        return document
            .Descendants(SpreadsheetNamespace + "si")
            .Select(item => string.Concat(item.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value)))
            .ToArray();
    }

    private static string NormalizeWorksheetTarget(string target)
    {
        var normalized = target.Replace('\\', '/').TrimStart('/');
        while (normalized.StartsWith("../", StringComparison.Ordinal))
        {
            normalized = normalized[3..];
        }

        return normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"xl/{normalized}";
    }

    private static WorksheetSummary AnalyzeWorksheet(
        string name,
        string state,
        ZipArchiveEntry entry,
        IReadOnlyList<string> sharedStrings,
        CancellationToken cancellationToken)
    {
        var document = LoadXml(entry);
        var dimension = document.Root?
            .Element(SpreadsheetNamespace + "dimension")?
            .Attribute("ref")?
            .Value;
        var mergedCellCount = document
            .Descendants(SpreadsheetNamespace + "mergeCell")
            .Count();

        var previewCells = new SortedDictionary<int, SortedDictionary<int, string>>();
        var nonEmptyCellCount = 0;
        var formulaCount = 0;
        var errorCellCount = 0;
        var maximumRow = 0;
        var maximumColumn = 0;

        foreach (var cell in document.Descendants(SpreadsheetNamespace + "c"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reference = cell.Attribute("r")?.Value;
            var (row, column) = ParseCellReference(reference);
            maximumRow = Math.Max(maximumRow, row);
            maximumColumn = Math.Max(maximumColumn, column);

            if (cell.Element(SpreadsheetNamespace + "f") is not null)
            {
                formulaCount++;
            }

            var type = cell.Attribute("t")?.Value;
            if (type == "e")
            {
                errorCellCount++;
            }

            var value = ReadCellValue(cell, type, sharedStrings);
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            nonEmptyCellCount++;
            if (row is > 0 and <= MaximumPreviewRows && column is > 0 and <= MaximumPreviewColumns)
            {
                if (!previewCells.TryGetValue(row, out var rowCells))
                {
                    rowCells = new SortedDictionary<int, string>();
                    previewCells[row] = rowCells;
                }

                rowCells[column] = value;
            }
        }

        var preview = BuildPreview(previewCells);
        return new WorksheetSummary(
            name,
            state,
            dimension,
            maximumRow,
            maximumColumn,
            nonEmptyCellCount,
            formulaCount,
            errorCellCount,
            mergedCellCount,
            preview);
    }

    private static string ReadCellValue(
        XElement cell,
        string? type,
        IReadOnlyList<string> sharedStrings)
    {
        if (type == "inlineStr")
        {
            return string.Concat(cell.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value));
        }

        var raw = cell.Element(SpreadsheetNamespace + "v")?.Value ?? string.Empty;
        if (type == "s" && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
        {
            return index >= 0 && index < sharedStrings.Count ? sharedStrings[index] : raw;
        }

        if (type == "b")
        {
            return raw == "1" ? "TRUE" : "FALSE";
        }

        return raw;
    }

    private static (int Row, int Column) ParseCellReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return (0, 0);
        }

        var column = 0;
        var index = 0;
        while (index < reference.Length && char.IsLetter(reference[index]))
        {
            column = checked(column * 26 + char.ToUpperInvariant(reference[index]) - 'A' + 1);
            index++;
        }

        var row = 0;
        if (index < reference.Length)
        {
            _ = int.TryParse(reference[index..], NumberStyles.Integer, CultureInfo.InvariantCulture, out row);
        }

        return (row, column);
    }

    private static string BuildPreview(SortedDictionary<int, SortedDictionary<int, string>> rows)
    {
        if (rows.Count == 0)
        {
            return "(no non-empty preview cells)\n";
        }

        var maximumColumn = rows.Values.SelectMany(row => row.Keys).DefaultIfEmpty(0).Max();
        var builder = new StringBuilder();
        foreach (var (_, row) in rows)
        {
            for (var column = 1; column <= maximumColumn; column++)
            {
                if (column > 1)
                {
                    builder.Append('\t');
                }

                if (row.TryGetValue(column, out var value))
                {
                    builder.Append(value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' '));
                }
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private sealed record WorksheetSummary(
        string Name,
        string State,
        string? Dimension,
        int RowCount,
        int ColumnCount,
        int NonEmptyCellCount,
        int FormulaCount,
        int ErrorCellCount,
        int MergedCellCount,
        string Preview);
}
