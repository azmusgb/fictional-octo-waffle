using Microsoft.Extensions.Options;

namespace WorkbenchStudio.Api.Services;

public sealed record ImportPaths(string Root, string Staging, string Originals, string Extracted, string Exports);

public sealed class FileStorageService
{
    private readonly string _rootPath;

    public FileStorageService(IOptions<WorkspaceOptions> options, IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        var configured = options.Value.RootPath;
        _rootPath = Path.GetFullPath(
            Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(environment.ContentRootPath, configured));

        Directory.CreateDirectory(_rootPath);
        Directory.CreateDirectory(Path.Combine(_rootPath, "projects"));
    }

    public string RootPath => _rootPath;
    public string DatabasePath => Path.Combine(_rootPath, "workbench.db");

    public string GetProjectRoot(Guid projectId) =>
        Path.Combine(_rootPath, "projects", projectId.ToString("N"));

    public ImportPaths EnsureImportPaths(Guid projectId, Guid importId)
    {
        var root = Path.Combine(GetProjectRoot(projectId), "imports", importId.ToString("N"));
        var paths = new ImportPaths(
            root,
            Path.Combine(root, "staging"),
            Path.Combine(root, "originals"),
            Path.Combine(root, "extracted"),
            Path.Combine(root, "exports"));

        Directory.CreateDirectory(paths.Staging);
        Directory.CreateDirectory(paths.Originals);
        Directory.CreateDirectory(paths.Extracted);
        Directory.CreateDirectory(paths.Exports);
        return paths;
    }

    public string GetSafeDestination(string rootDirectory, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var normalizedRelative = relativePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(normalizedRelative))
        {
            throw new InvalidDataException("Absolute paths are not permitted.");
        }

        var rootFullPath = Path.GetFullPath(rootDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var destination = Path.GetFullPath(Path.Combine(rootFullPath, normalizedRelative));

        if (!destination.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The path escapes the permitted workspace directory.");
        }

        return destination;
    }

    public static string SanitizeFileName(string fileName)
    {
        var baseName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return "unnamed-file";
        }

        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var safe = new string(baseName.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "unnamed-file" : safe;
    }

    public static string NormalizeRelativePath(string relativePath) =>
        relativePath.Replace('\\', '/').TrimStart('/');
}
