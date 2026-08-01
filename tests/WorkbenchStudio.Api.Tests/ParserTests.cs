using WorkbenchStudio.Api.Domain;
using WorkbenchStudio.Api.Parsing;

namespace WorkbenchStudio.Api.Tests;

public sealed class ParserTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"workbench-parser-tests-{Guid.NewGuid():N}");

    public ParserTests() => Directory.CreateDirectory(_tempRoot);

    [Fact]
    public async Task JsonParser_ReportsStructure()
    {
        var path = await WriteAsync("data.json", """{"name":"sample","items":[1,2,3]}""");
        var parser = new JsonArtifactParser();

        var result = await parser.ParseAsync(Context(path), CancellationToken.None);

        Assert.Equal(ArtifactParseStatus.Parsed, result.Status);
        Assert.Equal("builtin.json", result.ParserId);
        Assert.NotNull(result.StructureSummary);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public async Task JsonParser_CreatesFindingForMalformedJson()
    {
        var path = await WriteAsync("broken.json", "{\"name\":");
        var parser = new JsonArtifactParser();

        var result = await parser.ParseAsync(Context(path), CancellationToken.None);

        Assert.Equal(ArtifactParseStatus.Failed, result.Status);
        Assert.Contains(result.Findings, finding => finding.RuleId == "JSON_INVALID");
    }

    [Fact]
    public async Task CsvParser_DetectsInconsistentColumns()
    {
        var path = await WriteAsync("data.csv", "id,name\n1,Ada\n2\n");
        var parser = new CsvArtifactParser();

        var result = await parser.ParseAsync(Context(path), CancellationToken.None);

        Assert.Equal(ArtifactParseStatus.ParsedWithWarnings, result.Status);
        Assert.Contains(result.Findings, finding => finding.RuleId == "CSV_INCONSISTENT_COLUMNS");
    }

    [Fact]
    public async Task XmlParser_RejectsDtdProcessing()
    {
        var path = await WriteAsync("data.xml", "<!DOCTYPE root [<!ENTITY xxe SYSTEM 'file:///etc/passwd'>]><root>&xxe;</root>");
        var parser = new XmlArtifactParser();

        var result = await parser.ParseAsync(Context(path), CancellationToken.None);

        Assert.Equal(ArtifactParseStatus.Failed, result.Status);
        Assert.Contains(result.Findings, finding => finding.RuleId == "XML_INVALID");
    }

    [Fact]
    public async Task LogParser_CapturesErrorEvidence()
    {
        var path = await WriteAsync("service.log", "INFO Started\nERROR Processing failed\nSystem.Exception: sample\n");
        var parser = new TextLogArtifactParser();

        var result = await parser.ParseAsync(Context(path), CancellationToken.None);

        Assert.Equal(ArtifactParseStatus.ParsedWithWarnings, result.Status);
        Assert.Contains(result.Findings, finding => finding.RuleId == "LOG_ERROR_ENTRY" && finding.SourceLocation == "Line 2");
    }


    [Fact]
    public async Task ExcelParser_ReportsWorkbookStructureAndFormulas()
    {
        var path = Path.Combine(_tempRoot, "sample.xlsx");
        using (var archive = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create))
        {
            WriteArchiveEntry(archive, "xl/workbook.xml", """
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Summary" sheetId="1" r:id="rId1" /></sheets>
                </workbook>
                """);
            WriteArchiveEntry(archive, "xl/_rels/workbook.xml.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Target="worksheets/sheet1.xml" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" />
                </Relationships>
                """);
            WriteArchiveEntry(archive, "xl/sharedStrings.xml", """
                <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="2" uniqueCount="2">
                  <si><t>Name</t></si><si><t>Workbench</t></si>
                </sst>
                """);
            WriteArchiveEntry(archive, "xl/worksheets/sheet1.xml", """
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <dimension ref="A1:B2" />
                  <sheetData>
                    <row r="1"><c r="A1" t="s"><v>0</v></c><c r="B1" t="s"><v>1</v></c></row>
                    <row r="2"><c r="A2"><v>2</v></c><c r="B2"><f>A2*2</f><v>4</v></c></row>
                  </sheetData>
                </worksheet>
                """);
        }

        var parser = new ExcelArtifactParser();
        var result = await parser.ParseAsync(Context(path), CancellationToken.None);

        Assert.Equal(ArtifactParseStatus.ParsedWithWarnings, result.Status);
        Assert.Equal("builtin.xlsx", result.ParserId);
        Assert.Contains(result.Findings, finding => finding.RuleId == "XLSX_FORMULAS_PRESENT");
        Assert.NotNull(result.PreviewText);
        Assert.Contains("Workbench", result.PreviewText!);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }


    private static void WriteArchiveEntry(System.IO.Compression.ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    private async Task<string> WriteAsync(string name, string content)
    {
        var path = Path.Combine(_tempRoot, name);
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    private static ArtifactParseContext Context(string path)
    {
        var info = new FileInfo(path);
        return new ArtifactParseContext(path, info.Name, info.Extension, info.Length);
    }
}
