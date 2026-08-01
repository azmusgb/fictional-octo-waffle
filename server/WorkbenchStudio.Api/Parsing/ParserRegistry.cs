namespace WorkbenchStudio.Api.Parsing;

public sealed class ParserRegistry(IEnumerable<IArtifactParser> parsers)
{
    private readonly IReadOnlyList<IArtifactParser> _parsers = parsers.ToArray();

    public IReadOnlyList<string> ParserIds => _parsers.Select(parser => parser.ParserId).OrderBy(value => value).ToArray();

    public IArtifactParser? Resolve(ArtifactParseContext context) =>
        _parsers.FirstOrDefault(parser => parser.CanParse(context));
}
