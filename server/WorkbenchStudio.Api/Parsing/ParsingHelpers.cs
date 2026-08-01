using System.Text;

namespace WorkbenchStudio.Api.Parsing;

public static class ParsingHelpers
{
    public const int MaximumPreviewCharacters = 20_000;
    public const long MaximumTextParseBytes = 20 * 1024 * 1024;

    public static async Task<string> ReadTextWithLimitAsync(
        string path,
        int maximumCharacters,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 64 * 1024,
            leaveOpen: false);

        var buffer = new char[Math.Min(maximumCharacters, 16_384)];
        var builder = new StringBuilder(Math.Min(maximumCharacters, 32_768));
        while (builder.Length < maximumCharacters)
        {
            var remaining = maximumCharacters - builder.Length;
            var read = await reader.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), cancellationToken);
            if (read == 0)
            {
                break;
            }

            builder.Append(buffer, 0, read);
        }

        return builder.ToString();
    }

    public static string TrimEvidence(string value, int maximum = 500)
    {
        var normalized = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return normalized.Length <= maximum ? normalized : normalized[..maximum] + "…";
    }
}
