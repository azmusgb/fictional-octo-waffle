namespace WorkbenchStudio.Api.Services;

public sealed class WorkspaceOptions
{
    public const string SectionName = "Workspace";

    public string RootPath { get; set; } = ".workspace";
    public long MaximumUploadBytes { get; set; } = 1_073_741_824;
    public long MaximumSingleFileBytes { get; set; } = 104_857_600;
    public long MaximumExtractedBytes { get; set; } = 1_073_741_824;
    public int MaximumExtractedFiles { get; set; } = 5_000;
    public double MaximumCompressionRatio { get; set; } = 1_000;
    public string[] AllowedOrigins { get; set; } = ["http://localhost:5173"];
}
