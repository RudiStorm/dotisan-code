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
    private static readonly string[] ResourceActions = ["View", "Create", "Update", "Delete"];
    private static readonly string[] BasePermissions = ["ProfileView"];

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
        var authenticationEnabled = await IsAuthenticationEnabledAsync(root, cancellationToken);
        var permissionsPath = Path.Combine(apiDirectory, "Authorization", "Permissions.cs");

        if (new[] { modelPath, endpointPath, dbSetPath }.Any(File.Exists))
        {
            return ScaffoldingResult.Failed($"A '{resourceName}' resource already exists.");
        }

        if (!File.Exists(extensionPath))
        {
            return ScaffoldingResult.Failed("The generated endpoint extension was not found; the project may not be a supported Dotisan application.");
        }

        string? permissions = null;
        if (authenticationEnabled)
        {
            if (!File.Exists(permissionsPath))
            {
                return ScaffoldingResult.Failed("The generated authorization permission contract was not found; the project may not be a supported authenticated Dotisan application.");
            }

            permissions = await File.ReadAllTextAsync(permissionsPath, cancellationToken);
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
        await File.WriteAllTextAsync(endpointPath, Endpoint(identifier, resourceName, featureName, authenticationEnabled), cancellationToken);
        await File.WriteAllTextAsync(dbSetPath, DbSet(identifier, resourceName, featureName), cancellationToken);
        if (authenticationEnabled)
        {
            await File.WriteAllTextAsync(permissionsPath, AddResourcePermissions(permissions!, featureName), cancellationToken);
        }

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

    private static string Endpoint(string identifier, string resourceName, string featureName, bool authenticationEnabled) => $$"""
    using {{identifier}}.Api.Data;
    {{(authenticationEnabled ? $"using {identifier}.Api.Authorization;\n    using {identifier}.Api.Auditing;" : string.Empty)}}
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.EntityFrameworkCore;

    namespace {{identifier}}.Api.Features.{{featureName}};

    public static class {{resourceName}}Endpoints
    {
        public static void Map{{resourceName}}Endpoints(IEndpointRouteBuilder endpoints)
        {
            var collection = endpoints.MapGet("/api/{{featureName.ToLowerInvariant()}}", async (AppDbContext db, {{(authenticationEnabled ? "HttpContext httpContext, IAuditWriter audit, " : string.Empty)}}CancellationToken cancellationToken) =>
            {
                var entities = await db.{{featureName}}.AsNoTracking().ToListAsync(cancellationToken);
                {{(authenticationEnabled ? "await audit.RecordAsync(httpContext, \"" + featureName + "\", null, \"list\", new Dictionary<string, object?>(), cancellationToken);" : string.Empty)}}
                return entities;
            });
            {{(authenticationEnabled ? "collection.RequireAuthorization(Permissions." + featureName + "View);" : string.Empty)}}

            var create = endpoints.MapPost("/api/{{featureName.ToLowerInvariant()}}", async (Create{{resourceName}}Request request, AppDbContext db, {{(authenticationEnabled ? "HttpContext httpContext, IAuditWriter audit, " : string.Empty)}}CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Name)] = ["Name is required."] });
                }

                var entity = new {{resourceName}} { Name = request.Name.Trim() };
                db.{{featureName}}.Add(entity);
                await db.SaveChangesAsync(cancellationToken);
                {{(authenticationEnabled ? "await audit.RecordAsync(httpContext, \"" + resourceName + "\", entity.Id.ToString(), \"create\", new Dictionary<string, object?> { [\"Name\"] = entity.Name }, cancellationToken);" : string.Empty)}}
                return Results.Created($"/api/{{featureName.ToLowerInvariant()}}/{entity.Id}", entity);
            });
            {{(authenticationEnabled ? "create.RequireAuthorization(Permissions." + featureName + "Create);" : string.Empty)}}

            {{(authenticationEnabled ? $$"""
            var read = endpoints.MapGet("/api/{{featureName.ToLowerInvariant()}}/{id:guid}", async (Guid id, AppDbContext db, HttpContext httpContext, IAuditWriter audit, CancellationToken cancellationToken) =>
            {
                var entity = await db.{{featureName}}.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
                if (entity is null)
                {
                    return Results.NotFound();
                }

                await audit.RecordAsync(httpContext, "{{resourceName}}", entity.Id.ToString(), "read", new Dictionary<string, object?>(), cancellationToken);
                return Results.Ok(entity);
            });
            read.RequireAuthorization(Permissions.{{featureName}}View);

            var update = endpoints.MapPut("/api/{{featureName.ToLowerInvariant()}}/{id:guid}", async (Guid id, Update{{resourceName}}Request request, AppDbContext db, HttpContext httpContext, IAuditWriter audit, CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Name)] = ["Name is required."] });
                }

                var entity = await db.{{featureName}}.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
                if (entity is null)
                {
                    return Results.NotFound();
                }

                var oldName = entity.Name;
                entity.Name = request.Name.Trim();
                await db.SaveChangesAsync(cancellationToken);
                await audit.RecordAsync(httpContext, "{{resourceName}}", entity.Id.ToString(), "update", new Dictionary<string, object?> { ["Name"] = new { old = oldName, @new = entity.Name } }, cancellationToken);
                return Results.Ok(entity);
            });
            update.RequireAuthorization(Permissions.{{featureName}}Update);

            var delete = endpoints.MapDelete("/api/{{featureName.ToLowerInvariant()}}/{id:guid}", async (Guid id, AppDbContext db, HttpContext httpContext, IAuditWriter audit, CancellationToken cancellationToken) =>
            {
                var entity = await db.{{featureName}}.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
                if (entity is null)
                {
                    return Results.NotFound();
                }

                db.{{featureName}}.Remove(entity);
                await db.SaveChangesAsync(cancellationToken);
                await audit.RecordAsync(httpContext, "{{resourceName}}", entity.Id.ToString(), "delete", new Dictionary<string, object?>(), cancellationToken);
                return Results.NoContent();
            });
            delete.RequireAuthorization(Permissions.{{featureName}}Delete);
            """ : string.Empty)}}
        }

        public sealed record Create{{resourceName}}Request(string Name);
        {{(authenticationEnabled ? "public sealed record Update" + resourceName + "Request(string Name);" : string.Empty)}}
    }
    """;

    private static string AddResourcePermissions(string permissions, string featureName)
    {
        const string marker = "    // DOTISAN:RESOURCE_PERMISSIONS";
        const string allPrefix = "    public static IReadOnlyList<string> All { get; } = [";
        var markerIndex = permissions.IndexOf(marker, StringComparison.Ordinal);
        var allStart = permissions.IndexOf(allPrefix, StringComparison.Ordinal);
        if (markerIndex < 0 || allStart < 0)
        {
            throw new InvalidOperationException("The generated permission contract is missing its resource permission marker.");
        }

        var allEnd = permissions.IndexOf("];", allStart, StringComparison.Ordinal);
        if (allEnd < 0)
        {
            throw new InvalidOperationException("The generated permission contract has an invalid permission list.");
        }

        var permissionPrefix = featureName.ToLowerInvariant();
        var constantPrefix = featureName;
        var existingNames = permissions[(allStart + allPrefix.Length)..allEnd]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var newNames = ResourceActions.Select(action => constantPrefix + action).ToArray();
        var permissionsToAdd = ResourceActions
            .Select(action => $"    public const string {constantPrefix}{action} = \"{permissionPrefix}.{action.ToLowerInvariant()}\";")
            .ToArray();
        var updated = permissions.Insert(markerIndex, string.Join(Environment.NewLine, permissionsToAdd) + Environment.NewLine);
        var updatedAllStart = updated.IndexOf(allPrefix, StringComparison.Ordinal);
        var updatedAllEnd = updated.IndexOf("];", updatedAllStart, StringComparison.Ordinal);
        var names = string.Join(", ", BasePermissions.Concat(existingNames).Concat(newNames).Distinct(StringComparer.Ordinal));
        return updated.Remove(updatedAllStart, updatedAllEnd + 2 - updatedAllStart)
            .Insert(updatedAllStart, $"{allPrefix}{names}];");
    }

    private static async Task<bool> IsAuthenticationEnabledAsync(string root, CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(root, "dotisan.config");
        if (!File.Exists(configPath))
        {
            return false;
        }

        var lines = await File.ReadAllLinesAsync(configPath, cancellationToken);
        return lines.Any(line => line.Trim().Equals("authentication: enabled", StringComparison.OrdinalIgnoreCase));
    }

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
