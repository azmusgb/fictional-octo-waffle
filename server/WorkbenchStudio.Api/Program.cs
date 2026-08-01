using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using WorkbenchStudio.Api.Data;
using WorkbenchStudio.Api.Endpoints;
using WorkbenchStudio.Api.Parsing;
using WorkbenchStudio.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var workspaceOptions = builder.Configuration
    .GetSection(WorkspaceOptions.SectionName)
    .Get<WorkspaceOptions>() ?? new WorkspaceOptions();

builder.Services.Configure<WorkspaceOptions>(builder.Configuration.GetSection(WorkspaceOptions.SectionName));
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = workspaceOptions.MaximumUploadBytes;
    options.ValueLengthLimit = 16 * 1024;
    options.MultipartHeadersLengthLimit = 32 * 1024;
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = workspaceOptions.MaximumUploadBytes;
});

builder.Services.AddSingleton<FileStorageService>();
builder.Services.AddSingleton<HashingService>();
builder.Services.AddSingleton<IImportQueue, ImportQueue>();
builder.Services.AddSingleton<ParserRegistry>();
builder.Services.AddSingleton<ImportProcessor>();
builder.Services.AddScoped<ComparisonService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<ProjectManifestService>();
builder.Services.AddHostedService<ImportWorkerService>();

builder.Services.AddSingleton<IArtifactParser, JsonArtifactParser>();
builder.Services.AddSingleton<IArtifactParser, CsvArtifactParser>();
builder.Services.AddSingleton<IArtifactParser, XmlArtifactParser>();
builder.Services.AddSingleton<IArtifactParser, TextLogArtifactParser>();
builder.Services.AddSingleton<IArtifactParser, ExcelArtifactParser>();

var bootstrapStorage = new FileStorageService(
    Microsoft.Extensions.Options.Options.Create(workspaceOptions),
    builder.Environment);
builder.Services.AddDbContextFactory<WorkbenchDbContext>(options =>
    options.UseSqlite($"Data Source={bootstrapStorage.DatabasePath};Cache=Shared"));

builder.Services.AddProblemDetails();
builder.Services.AddCors(options =>
{
    var configuredOrigins = workspaceOptions.AllowedOrigins
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Select(origin => origin.Trim().TrimEnd('/'))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    if (configuredOrigins.Length == 0)
    {
        configuredOrigins = ["http://localhost:5173"];
    }

    options.AddPolicy("WorkbenchClient", policy =>
        policy.WithOrigins(configuredOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetPreflightMaxAge(TimeSpan.FromHours(1)));
});

var app = builder.Build();

app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");

    var privateNetworkRequest = string.Equals(
        context.Request.Headers["Access-Control-Request-Private-Network"],
        "true",
        StringComparison.OrdinalIgnoreCase);
    if (privateNetworkRequest)
    {
        context.Response.OnStarting(() =>
        {
            if (context.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
            {
                context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
            }
            return Task.CompletedTask;
        });
    }

    await next();
});
app.UseCors("WorkbenchClient");

await using (var scope = app.Services.CreateAsyncScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>();
    await DatabaseBootstrapper.InitializeAsync(factory);
}

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "Workbench Studio Local Agent",
    version = "3.0.0",
    timestampUtc = DateTimeOffset.UtcNow
}));
app.MapSystemEndpoints();
app.MapProjectsEndpoints();
app.MapImportsEndpoints();
app.MapArtifactsEndpoints();

var webRoot = app.Environment.WebRootPath;
if (!string.IsNullOrWhiteSpace(webRoot) && Directory.Exists(webRoot))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}
else
{
    app.MapGet("/", () => Results.Text(
        "Workbench Studio Local Agent v3 is running. Start the Vite client, use the hosted shell, or build the client into wwwroot.",
        "text/plain"));
}

app.Run();

public partial class Program
{
}
