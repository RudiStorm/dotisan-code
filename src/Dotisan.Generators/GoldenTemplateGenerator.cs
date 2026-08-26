using Dotisan.Core;

namespace Dotisan.Generators;

public sealed class GoldenTemplateGenerator : IProjectGenerator
{
    public async Task<GenerationResult> GenerateAsync(ProjectOptions options, CancellationToken cancellationToken)
    {
        string outputDirectory;
        try
        {
            outputDirectory = Path.GetFullPath(options.OutputDirectory);
            if (Directory.Exists(outputDirectory) && Directory.EnumerateFileSystemEntries(outputDirectory).Any())
            {
                return GenerationResult.Failed($"Output directory '{outputDirectory}' already exists and is not empty.");
            }

            Directory.CreateDirectory(outputDirectory);
            var identifier = ToIdentifier(options.Name);
            foreach (var file in TemplateFiles.Create(options, identifier))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = Path.Combine(outputDirectory, file.Path.Replace('/', Path.DirectorySeparatorChar));
                var directory = Path.GetDirectoryName(path);
                if (directory is not null)
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(path, file.Content, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return GenerationResult.Failed($"Could not create the project: {exception.Message}");
        }

        return GenerationResult.Succeeded(outputDirectory);
    }

    private static string ToIdentifier(string projectName)
    {
        var identifier = new string(projectName.Select(character => char.IsLetterOrDigit(character) || character == '_' ? character : '_').ToArray());
        return char.IsLetter(identifier[0]) ? identifier : $"App_{identifier}";
    }
}

public sealed record ScaffoldingResult(bool Success, string? ErrorMessage)
{
    public static ScaffoldingResult Succeeded() => new(true, null);

    public static ScaffoldingResult Failed(string message) => new(false, message);
}

public sealed class ResourceScaffolder
{
    public static async Task<ScaffoldingResult> ScaffoldAsync(string projectDirectory, string resourceName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resourceName) || !IsIdentifier(resourceName))
        {
            return ScaffoldingResult.Failed("Resource names must be valid C# identifiers, for example 'Customer'.");
        }

        var root = Path.GetFullPath(projectDirectory);
        if (!Directory.Exists(root))
        {
            return ScaffoldingResult.Failed($"Project directory '{root}' does not exist.");
        }

        var apiProject = Directory.EnumerateFiles(root, "*.Api.csproj", SearchOption.AllDirectories)
            .FirstOrDefault(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
        if (apiProject is null)
        {
            return ScaffoldingResult.Failed("Could not find an API project. Run this command from a generated Dotisan project.");
        }

        var apiDirectory = Path.GetDirectoryName(apiProject)!;
        var projectName = Path.GetFileNameWithoutExtension(apiProject)[..^4];
        var identifier = ToIdentifier(projectName);
        var featureName = Pluralize(resourceName);
        var featureDirectory = Path.Combine(apiDirectory, "Features", featureName);
        var modelPath = Path.Combine(featureDirectory, $"{resourceName}.cs");
        var endpointPath = Path.Combine(featureDirectory, $"{resourceName}Endpoints.cs");
        var dbSetPath = Path.Combine(apiDirectory, "Data", $"{resourceName}DbSet.cs");
        var extensionPath = Path.Combine(apiDirectory, "Infrastructure", "DotisanEndpointExtensions.cs");

        if (new[] { modelPath, endpointPath, dbSetPath }.Any(File.Exists))
        {
            return ScaffoldingResult.Failed($"A '{resourceName}' resource already exists.");
        }

        if (!File.Exists(extensionPath))
        {
            return ScaffoldingResult.Failed("The generated endpoint extension was not found; the project may not be a supported Dotisan application.");
        }

        var extension = await File.ReadAllTextAsync(extensionPath, cancellationToken);
        const string marker = "// DOTISAN:ENDPOINTS";
        var markerIndex = extension.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return ScaffoldingResult.Failed("The endpoint extension is missing its Dotisan registration marker.");
        }

        Directory.CreateDirectory(featureDirectory);
        await File.WriteAllTextAsync(modelPath, Model(identifier, resourceName, featureName), cancellationToken);
        await File.WriteAllTextAsync(endpointPath, Endpoint(identifier, resourceName, featureName), cancellationToken);
        await File.WriteAllTextAsync(dbSetPath, DbSet(identifier, resourceName, featureName), cancellationToken);
        var lineEnd = extension.IndexOf('\n', markerIndex);
        lineEnd = lineEnd < 0 ? extension.Length : lineEnd;
        var registration = $"\n        global::{identifier}.Api.Features.{featureName}.{resourceName}Endpoints.Map{resourceName}Endpoints(endpoints);";
        extension = extension.Insert(lineEnd, registration);
        await File.WriteAllTextAsync(extensionPath, extension, cancellationToken);

        return ScaffoldingResult.Succeeded();
    }

    private static bool IsIdentifier(string value) => char.IsLetter(value[0]) && value.All(character => char.IsLetterOrDigit(character) || character == '_');

    private static string Pluralize(string value) => value.EndsWith('y') && value.Length > 1
        ? value[..^1] + "ies"
        : value.EndsWith('s') ? value : value + "s";

    private static string ToIdentifier(string projectName) => new(projectName.Select(character => char.IsLetterOrDigit(character) || character == '_' ? character : '_').ToArray());

    private static string Model(string identifier, string resourceName, string featureName) => $$"""
    namespace {{identifier}}.Api.Features.{{featureName}};

    public sealed class {{resourceName}}
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
    """;

    private static string Endpoint(string identifier, string resourceName, string featureName) => $$"""
    using {{identifier}}.Api.Data;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.EntityFrameworkCore;

    namespace {{identifier}}.Api.Features.{{featureName}};

    public static class {{resourceName}}Endpoints
    {
        public static void Map{{resourceName}}Endpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/api/{{featureName.ToLowerInvariant()}}", async (AppDbContext db, CancellationToken cancellationToken) =>
                await db.{{featureName}}.AsNoTracking().ToListAsync(cancellationToken));

            endpoints.MapPost("/api/{{featureName.ToLowerInvariant()}}", async (Create{{resourceName}}Request request, AppDbContext db, CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Name)] = ["Name is required."] });
                }

                var entity = new {{resourceName}} { Name = request.Name.Trim() };
                db.{{featureName}}.Add(entity);
                await db.SaveChangesAsync(cancellationToken);
                return Results.Created($"/api/{{featureName.ToLowerInvariant()}}/{entity.Id}", entity);
            });
        }

        public sealed record Create{{resourceName}}Request(string Name);
    }
    """;

    private static string DbSet(string identifier, string resourceName, string featureName) => $$"""
    using Microsoft.EntityFrameworkCore;
    using {{identifier}}.Api.Features.{{featureName}};

    namespace {{identifier}}.Api.Data;

    public sealed partial class AppDbContext
    {
        public DbSet<{{resourceName}}> {{featureName}} => Set<{{resourceName}}>();
    }
    """;
}

public static class EndpointScaffolder
{
    public static async Task<ScaffoldingResult> ScaffoldAsync(string projectDirectory, string endpointName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(endpointName) || !char.IsLetter(endpointName[0]) || endpointName.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
            return ScaffoldingResult.Failed("Endpoint names must be valid C# identifiers, for example 'CreateCustomer'.");

        var root = Path.GetFullPath(projectDirectory);
        var apiProject = Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*.Api.csproj", SearchOption.AllDirectories).FirstOrDefault(path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase) && !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase))
            : null;
        if (apiProject is null)
            return ScaffoldingResult.Failed("Could not find an API project. Run this command from a generated Dotisan project.");

        var apiDirectory = Path.GetDirectoryName(apiProject)!;
        var projectName = Path.GetFileNameWithoutExtension(apiProject)[..^4];
        var identifier = new string(projectName.Select(character => char.IsLetterOrDigit(character) || character == '_' ? character : '_').ToArray());
        var resourceName = endpointName.StartsWith("Create", StringComparison.Ordinal) ? endpointName[6..] : endpointName.StartsWith("Get", StringComparison.Ordinal) ? endpointName[3..] : endpointName;
        resourceName = string.IsNullOrWhiteSpace(resourceName) ? "Endpoint" : resourceName;
        var featureName = resourceName.EndsWith('s') ? resourceName : resourceName + "s";
        var featureDirectory = Path.Combine(apiDirectory, "Features", featureName);
        var endpointPath = Path.Combine(featureDirectory, endpointName + ".cs");
        var extensionPath = Path.Combine(apiDirectory, "Infrastructure", "DotisanEndpointExtensions.cs");
        if (File.Exists(endpointPath))
            return ScaffoldingResult.Failed($"An endpoint named '{endpointName}' already exists.");
        if (!File.Exists(extensionPath))
            return ScaffoldingResult.Failed("The generated endpoint extension was not found; the project may not be a supported Dotisan application.");

        var extension = await File.ReadAllTextAsync(extensionPath, cancellationToken);
        const string marker = "// DOTISAN:ENDPOINTS";
        var markerIndex = extension.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            return ScaffoldingResult.Failed("The endpoint extension is missing its Dotisan registration marker.");

        Directory.CreateDirectory(featureDirectory);
        await File.WriteAllTextAsync(endpointPath, Endpoint(identifier, endpointName, featureName), cancellationToken);
        var lineEnd = extension.IndexOf('\n', markerIndex);
        lineEnd = lineEnd < 0 ? extension.Length : lineEnd;
        extension = extension.Insert(lineEnd, $"\n        global::{identifier}.Api.Features.{featureName}.{endpointName}.Map(endpoints);");
        await File.WriteAllTextAsync(extensionPath, extension, cancellationToken);
        return ScaffoldingResult.Succeeded();
    }

    private static string Endpoint(string identifier, string endpointName, string featureName) => $$"""
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Routing;

    namespace {{identifier}}.Api.Features.{{featureName}};

    public static class {{endpointName}}
    {
        public static void Map(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/api/{{featureName.ToLowerInvariant()}}", () => Results.Ok(new { message = "{{endpointName}} is ready" }));
        }
    }
    """;
}
