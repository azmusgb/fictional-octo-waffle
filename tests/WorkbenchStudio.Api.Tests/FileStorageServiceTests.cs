using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using WorkbenchStudio.Api.Services;

namespace WorkbenchStudio.Api.Tests;

public sealed class FileStorageServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"workbench-tests-{Guid.NewGuid():N}");

    [Fact]
    public void GetSafeDestination_AllowsPathInsideWorkspace()
    {
        var service = CreateService();
        var root = Path.Combine(_tempRoot, "extract");
        Directory.CreateDirectory(root);

        var result = service.GetSafeDestination(root, "folder/data.json");

        Assert.True(result.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase));
        Assert.True(result.EndsWith(Path.Combine("folder", "data.json"), StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("folder/../../outside.txt")]
    public void GetSafeDestination_RejectsTraversal(string relativePath)
    {
        var service = CreateService();
        var root = Path.Combine(_tempRoot, "extract");
        Directory.CreateDirectory(root);

        Assert.Throws<InvalidDataException>(() => service.GetSafeDestination(root, relativePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private FileStorageService CreateService()
    {
        Directory.CreateDirectory(_tempRoot);
        return new FileStorageService(
            Options.Create(new WorkspaceOptions { RootPath = Path.Combine(_tempRoot, ".workspace") }),
            new TestEnvironment(_tempRoot));
    }

    private sealed class TestEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "WorkbenchStudio.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
