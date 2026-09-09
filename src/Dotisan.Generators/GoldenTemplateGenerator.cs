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

            var frontendDirectory = Path.Combine(outputDirectory, "src", $"{options.Name}.Web");
            await GeneratedContractWriter.WriteAsync(
                frontendDirectory,
                TemplateFiles.InitialContractManifest(options.AuthenticationEnabled),
                cancellationToken);
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
        var multiTenancyEnabled = await IsMultiTenancyEnabledAsync(root, cancellationToken);
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
        await File.WriteAllTextAsync(modelPath, Model(identifier, resourceName, featureName, multiTenancyEnabled), cancellationToken);
        await File.WriteAllTextAsync(endpointPath, Endpoint(identifier, resourceName, featureName, authenticationEnabled, multiTenancyEnabled), cancellationToken);
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

    private static string Model(string identifier, string resourceName, string featureName, bool multiTenancyEnabled) => $$"""
    {{(multiTenancyEnabled ? $"using {identifier}.Api.Tenancy;" : string.Empty)}}

    namespace {{identifier}}.Api.Features.{{featureName}};

    public sealed class {{resourceName}}{{(multiTenancyEnabled ? " : ITenantEntity" : string.Empty)}}
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        {{(multiTenancyEnabled ? "public string TenantId { get; set; } = string.Empty;" : string.Empty)}}
    }
    """;

    private static string Endpoint(string identifier, string resourceName, string featureName, bool authenticationEnabled, bool multiTenancyEnabled) => $$"""
    {{(multiTenancyEnabled ? $"using {identifier}.Api.Tenancy;" : string.Empty)}}
    using {{identifier}}.Api.Data;
    {{(authenticationEnabled ? $"using {identifier}.Api.Authorization;\n    using {identifier}.Api.Auditing;" : string.Empty)}}
    using System.ComponentModel.DataAnnotations;
    using Microsoft.AspNetCore.Http;
    {{(authenticationEnabled ? "using Microsoft.AspNetCore.Antiforgery;" : string.Empty)}}
    using Microsoft.AspNetCore.Routing;
    using Microsoft.EntityFrameworkCore;

    namespace {{identifier}}.Api.Features.{{featureName}};

    public static class {{resourceName}}Endpoints
    {
        public static void Map{{resourceName}}Endpoints(IEndpointRouteBuilder endpoints)
        {
            var collection = endpoints.MapGet("/api/{{featureName.ToLowerInvariant()}}", async (AppDbContext db, {{(multiTenancyEnabled ? "ITenantContext tenantContext, " : string.Empty)}}{{(authenticationEnabled ? "HttpContext httpContext, IAuditWriter audit, " : string.Empty)}}CancellationToken cancellationToken) =>
            {
                var entities = await db.{{featureName}}.AsNoTracking(){{(multiTenancyEnabled ? ".Where(item => item.TenantId == tenantContext.TenantId)" : string.Empty)}}.ToListAsync(cancellationToken);
                {{(authenticationEnabled ? "await audit.RecordAsync(httpContext, \"" + featureName + "\", null, \"list\", new Dictionary<string, object?>(), cancellationToken);" : string.Empty)}}
                return entities;
            }).WithName("List{{resourceName}}").WithTags("{{resourceName}}");
            {{(authenticationEnabled ? "collection.RequireAuthorization(Permissions." + featureName + "View);" : string.Empty)}}

            var create = endpoints.MapPost("/api/{{featureName.ToLowerInvariant()}}", async (Create{{resourceName}}Request request, AppDbContext db, {{(multiTenancyEnabled ? "ITenantContext tenantContext, " : string.Empty)}}{{(authenticationEnabled ? "HttpContext httpContext, IAuditWriter audit, " : string.Empty)}}CancellationToken cancellationToken) =>
            {
                if (!TryValidate(request, out var validationErrors))
                {
                    return Results.ValidationProblem(validationErrors);
                }

                var entity = new {{resourceName}} { Name = request.Name!.Trim(){{(multiTenancyEnabled ? ", TenantId = tenantContext.RequireTenantId()" : string.Empty)}} };
                db.{{featureName}}.Add(entity);
                await db.SaveChangesAsync(cancellationToken);
                {{(authenticationEnabled ? "await audit.RecordAsync(httpContext, \"" + resourceName + "\", entity.Id.ToString(), \"create\", new Dictionary<string, object?> { [\"Name\"] = entity.Name }, cancellationToken);" : string.Empty)}}
                return Results.Created($"/api/{{featureName.ToLowerInvariant()}}/{entity.Id}", entity);
            }).WithName("Create{{resourceName}}").WithTags("{{resourceName}}");
            {{(authenticationEnabled ? "create.RequireAuthorization(Permissions." + featureName + "Create).WithMetadata(new RequireAntiforgeryTokenAttribute(true));" : string.Empty)}}

            {{(authenticationEnabled ? $$"""
            var read = endpoints.MapGet("/api/{{featureName.ToLowerInvariant()}}/{id:guid}", async (Guid id, AppDbContext db, {{(multiTenancyEnabled ? "ITenantContext tenantContext, " : string.Empty)}}HttpContext httpContext, IAuditWriter audit, CancellationToken cancellationToken) =>
            {
                var entity = await db.{{featureName}}.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id{{(multiTenancyEnabled ? " && item.TenantId == tenantContext.TenantId" : string.Empty)}}, cancellationToken);
                if (entity is null)
                {
                    return Results.NotFound();
                }

                await audit.RecordAsync(httpContext, "{{resourceName}}", entity.Id.ToString(), "read", new Dictionary<string, object?>(), cancellationToken);
                return Results.Ok(entity);
            }).WithName("Get{{resourceName}}").WithTags("{{resourceName}}");
            read.RequireAuthorization(Permissions.{{featureName}}View);

            var update = endpoints.MapPut("/api/{{featureName.ToLowerInvariant()}}/{id:guid}", async (Guid id, Update{{resourceName}}Request request, AppDbContext db, {{(multiTenancyEnabled ? "ITenantContext tenantContext, " : string.Empty)}}HttpContext httpContext, IAuditWriter audit, CancellationToken cancellationToken) =>
            {
                if (!TryValidate(request, out var validationErrors))
                {
                    return Results.ValidationProblem(validationErrors);
                }

                var entity = await db.{{featureName}}.FirstOrDefaultAsync(item => item.Id == id{{(multiTenancyEnabled ? " && item.TenantId == tenantContext.TenantId" : string.Empty)}}, cancellationToken);
                if (entity is null)
                {
                    return Results.NotFound();
                }

                var oldName = entity.Name;
                entity.Name = request.Name!.Trim();
                await db.SaveChangesAsync(cancellationToken);
                await audit.RecordAsync(httpContext, "{{resourceName}}", entity.Id.ToString(), "update", new Dictionary<string, object?> { ["Name"] = new { old = oldName, @new = entity.Name } }, cancellationToken);
                return Results.Ok(entity);
            }).WithName("Update{{resourceName}}").WithTags("{{resourceName}}");
            update.RequireAuthorization(Permissions.{{featureName}}Update).WithMetadata(new RequireAntiforgeryTokenAttribute(true));

            var delete = endpoints.MapDelete("/api/{{featureName.ToLowerInvariant()}}/{id:guid}", async (Guid id, AppDbContext db, {{(multiTenancyEnabled ? "ITenantContext tenantContext, " : string.Empty)}}HttpContext httpContext, IAuditWriter audit, CancellationToken cancellationToken) =>
            {
                var entity = await db.{{featureName}}.FirstOrDefaultAsync(item => item.Id == id{{(multiTenancyEnabled ? " && item.TenantId == tenantContext.TenantId" : string.Empty)}}, cancellationToken);
                if (entity is null)
                {
                    return Results.NotFound();
                }

                db.{{featureName}}.Remove(entity);
                await db.SaveChangesAsync(cancellationToken);
                await audit.RecordAsync(httpContext, "{{resourceName}}", entity.Id.ToString(), "delete", new Dictionary<string, object?>(), cancellationToken);
                return Results.NoContent();
            }).WithName("Delete{{resourceName}}").WithTags("{{resourceName}}");
            delete.RequireAuthorization(Permissions.{{featureName}}Delete).WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            """ : string.Empty)}}
        }

        private static bool TryValidate<T>(T request, out Dictionary<string, string[]> errors)
        {
            var results = new List<ValidationResult>();
            var valid = Validator.TryValidateObject(request!, new ValidationContext(request!), results, validateAllProperties: true);
            errors = results
                .SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty).Select(member => new { member, message = result.ErrorMessage ?? "The value is invalid." }))
                .GroupBy(item => item.member, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Select(item => item.message).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.OrdinalIgnoreCase);
            return valid;
        }

        public sealed record Create{{resourceName}}Request([property: Required, StringLength(200)] string? Name);
        {{(authenticationEnabled ? "public sealed record Update" + resourceName + "Request([property: Required, StringLength(200)] string? Name);" : string.Empty)}}
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

    private static async Task<bool> IsMultiTenancyEnabledAsync(string root, CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(root, "dotisan.config");
        if (!File.Exists(configPath))
            return false;

        var lines = await File.ReadAllLinesAsync(configPath, cancellationToken);
        return lines.Any(line => line.Trim().Equals("multi_tenancy: enabled", StringComparison.OrdinalIgnoreCase));
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
