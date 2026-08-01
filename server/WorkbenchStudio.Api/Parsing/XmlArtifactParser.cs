using System.Xml;
using System.Xml.Linq;
using WorkbenchStudio.Api.Domain;

namespace WorkbenchStudio.Api.Parsing;

public sealed class XmlArtifactParser : IArtifactParser
{
    public string ParserId => "builtin.xml";
    public string ParserVersion => "1.0.0";

    public bool CanParse(ArtifactParseContext context) =>
        context.Extension.Equals(".xml", StringComparison.OrdinalIgnoreCase);

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
                new { Kind = "xml", Parsed = false, Reason = "File exceeds the XML parse limit." },
                null,
                [new ParserFinding(
                    FindingSeverity.Warning,
                    "XML_FILE_TOO_LARGE",
                    "XML file exceeds parse limit",
                    $"The file is {context.SizeBytes:N0} bytes and was inventoried without loading the full XML tree.")]);
        }

        try
        {
            var settings = new XmlReaderSettings
            {
                Async = true,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersFromEntities = 0,
                IgnoreComments = false,
                IgnoreWhitespace = false
            };

            await using var stream = File.OpenRead(context.FilePath);
            using var reader = XmlReader.Create(stream, settings);
            var document = await XDocument.LoadAsync(reader, LoadOptions.SetLineInfo, cancellationToken);
            var elements = document.Descendants().ToArray();
            var attributes = elements.SelectMany(x => x.Attributes()).Count();
            var maximumDepth = elements.Length == 0
                ? 0
                : elements.Max(GetDepth);
            var names = elements
                .GroupBy(x => x.Name.LocalName, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .Take(50)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

            var findings = new List<ParserFinding>();
            if (document.Root is null)
            {
                findings.Add(new ParserFinding(
                    FindingSeverity.Error,
                    "XML_ROOT_MISSING",
                    "XML root element missing",
                    "The XML document does not contain a root element."));
            }

            if (maximumDepth > 100)
            {
                findings.Add(new ParserFinding(
                    FindingSeverity.Warning,
                    "XML_DEEP_NESTING",
                    "Deep XML hierarchy",
                    $"The document reaches a nesting depth of {maximumDepth}."));
            }

            var preview = document.ToString(SaveOptions.None);
            if (preview.Length > ParsingHelpers.MaximumPreviewCharacters)
            {
                preview = preview[..ParsingHelpers.MaximumPreviewCharacters] + "\n… preview truncated …";
            }

            return new ArtifactParseResult(
                findings.Any(x => x.Severity == FindingSeverity.Error)
                    ? ArtifactParseStatus.Failed
                    : findings.Count == 0
                        ? ArtifactParseStatus.Parsed
                        : ArtifactParseStatus.ParsedWithWarnings,
                ParserId,
                ParserVersion,
                new
                {
                    Kind = "xml",
                    RootElement = document.Root?.Name.LocalName,
                    Namespace = document.Root?.Name.NamespaceName,
                    ElementCount = elements.Length,
                    AttributeCount = attributes,
                    MaximumDepth = maximumDepth,
                    ElementFrequency = names
                },
                preview,
                findings);
        }
        catch (XmlException exception)
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
                    "XML_INVALID",
                    "Invalid XML",
                    exception.Message,
                    $"Line {exception.LineNumber}, position {exception.LinePosition}",
                    Recommendation: "Correct the malformed XML and import a new snapshot.")],
                exception.Message);
        }
    }

    private static int GetDepth(XElement element)
    {
        var depth = 0;
        var parent = element.Parent;
        while (parent is not null)
        {
            depth++;
            parent = parent.Parent;
        }

        return depth;
    }
}
