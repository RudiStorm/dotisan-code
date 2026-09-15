using System.Text;
using Dotisan.Core;

namespace Dotisan.TypeScript;

public sealed record GeneratedTypeScriptFile(string Path, string Content);

public static class TypeScriptContractGenerator
{
    public static IReadOnlyList<GeneratedTypeScriptFile> GenerateAll(ContractManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var groups = manifest.Endpoints
            .Select((endpoint, index) => (Endpoint: endpoint, Metadata: manifest.EndpointMetadata?[index]))
            .GroupBy(item => FeatureSlug(item.Endpoint, item.Metadata), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();
        if (groups.Length == 0)
            return [Generate(manifest)[0], ZodSchemaGenerator.Generate(manifest), FetchClientGenerator.Generate(manifest), TanStackQueryGenerator.Generate(manifest)];
        var files = new List<GeneratedTypeScriptFile>();
        foreach (var group in groups)
        {
            var endpoints = group.Select(item => item.Endpoint).ToArray();
            var metadata = manifest.EndpointMetadata is null ? null : group.Select(item => item.Metadata!).ToArray();
            var featureModels = SelectFeatureModels(manifest.Models, endpoints);
            var featureManifest = new ContractManifest(manifest.SchemaVersion, endpoints, featureModels, metadata);
            var prefix = $"features/{group.Key}";
            files.Add(Generate(featureManifest, $"{prefix}/models.ts"));
            files.Add(ZodSchemaGenerator.Generate(featureManifest, $"{prefix}/schemas.ts"));
            files.Add(FetchClientGenerator.Generate(featureManifest, $"{prefix}/services.ts", "./models"));
            files.Add(TanStackQueryGenerator.Generate(featureManifest, $"{prefix}/queries.ts", "./services"));
            files.Add(new GeneratedTypeScriptFile($"{prefix}/index.ts", "export * from \"./models\";\nexport * from \"./schemas\";\nexport * from \"./services\";\nexport * from \"./queries\";\n"));
        }

        files.Add(Barrel("models.ts", groups.AsEnumerable().Select(group => $"./features/{group.Key}/models")));
        files.Add(Barrel("schemas.ts", groups.AsEnumerable().Select(group => $"./features/{group.Key}/schemas")));
        files.Add(Barrel("services.ts", groups.AsEnumerable().Select(group => $"./features/{group.Key}/services")));
        files.Add(Barrel("queries.ts", groups.AsEnumerable().Select(group => $"./features/{group.Key}/queries")));
        return files;
    }

    public static IReadOnlyList<GeneratedTypeScriptFile> Generate(ContractManifest manifest)
        => [Generate(manifest, "models.ts")];

    internal static GeneratedTypeScriptFile Generate(ContractManifest manifest, string path)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var models = manifest.Models.OrderBy(model => model.Name, StringComparer.Ordinal).ToArray();
        var builder = new StringBuilder();
        var hasWrittenSection = false;

        foreach (var model in models.Where(model => model.EnumValues.Count > 0))
        {
            if (hasWrittenSection)
            {
                builder.AppendLine();
            }

            AppendEnum(builder, model);
            hasWrittenSection = true;
        }

        foreach (var model in models.Where(model => model.EnumValues.Count == 0))
        {
            if (hasWrittenSection)
            {
                builder.AppendLine();
            }

            AppendInterface(builder, model);
            hasWrittenSection = true;
        }

        if (builder.Length == 0)
        {
            builder.AppendLine();
        }

        return new GeneratedTypeScriptFile(path, builder.ToString());
    }

    private static GeneratedTypeScriptFile Barrel(string path, IEnumerable<string> imports)
    {
        var builder = new StringBuilder();
        foreach (var import in imports)
            builder.Append("export * from \"").Append(import).AppendLine("\";");
        return new GeneratedTypeScriptFile(path, builder.ToString());
    }

    private static ContractModel[] SelectFeatureModels(
        IReadOnlyList<ContractModel> models,
        IReadOnlyList<EndpointManifestEntry> endpoints)
    {
        var byName = models.ToDictionary(model => model.Name, StringComparer.Ordinal);
        var selected = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>(models
            .Where(model => endpoints.Any(endpoint => endpoint.Request.Contains(model.Name, StringComparison.Ordinal) || endpoint.Response.Contains(model.Name, StringComparison.Ordinal)))
            .Select(model => model.Name));

        while (pending.Count > 0)
        {
            var name = pending.Dequeue();
            if (!selected.Add(name) || !byName.TryGetValue(name, out var model))
                continue;

            foreach (var property in model.Properties)
                EnqueueReferences(property.Type, byName, pending);
        }

        return models.Where(model => selected.Contains(model.Name)).ToArray();
    }

    private static void EnqueueReferences(ContractTypeDescriptor type, IReadOnlyDictionary<string, ContractModel> models, Queue<string> pending)
    {
        if (type.ReferenceName is not null && models.ContainsKey(type.ReferenceName))
            pending.Enqueue(type.ReferenceName);
        if (type.ElementType is not null)
            EnqueueReferences(type.ElementType, models, pending);
    }

    private static string FeatureSlug(EndpointManifestEntry endpoint, EndpointContractMetadata? metadata)
    {
        var source = endpoint.Feature;
        if (string.IsNullOrWhiteSpace(source))
        {
            var tags = metadata?.Tags;
            var idParts = endpoint.Id.Split('.', StringSplitOptions.RemoveEmptyEntries);
            source = tags is { Count: > 0 } ? tags[0] : idParts.Length > 0 ? idParts[0] : "shared";
        }
        var builder = new StringBuilder();
        foreach (var character in source.Trim().ToLowerInvariant())
            builder.Append(char.IsLetterOrDigit(character) ? character : '-');
        var slug = builder.ToString().Trim('-');
        return slug switch
        {
            "account" or "authentication" or "authorization" or "security" => "auth",
            { Length: > 0 } => slug,
            _ => "shared"
        };
    }

    private static void AppendEnum(StringBuilder builder, ContractModel model)
    {
        builder.Append("export enum ").Append(model.Name).AppendLine(" {");

        foreach (var value in model.EnumValues)
        {
            builder.Append("  ").Append(value.Name).Append(" = ").Append(value.Value).AppendLine(",");
        }

        builder.AppendLine("}");
    }

    private static void AppendInterface(StringBuilder builder, ContractModel model)
    {
        builder.Append("export interface ").Append(model.Name).AppendLine(" {");

        foreach (var property in model.Properties.OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            var mappedType = TypeScriptTypeMapper.Map(property.Type, property.Nullable, property.Optional);
            builder.Append("  ").Append(property.Name).Append(": ").Append(mappedType).AppendLine(";");
        }

        builder.AppendLine("}");
    }
}
