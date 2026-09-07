using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dotisan.Core;

namespace Dotisan.OpenApi;

public sealed record OpenApiDocumentResult(string Json, string Sha256);

public static class OpenApiDocumentGenerator
{
    private const string JsonSchemaDialect = "https://spec.openapis.org/oas/3.1/dialect/base";

    public static OpenApiDocumentResult Generate(ContractManifest manifest, string title, string version)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var metadata = manifest.EndpointMetadata
            ?? (manifest.Endpoints.Count == 0
                ? Array.Empty<EndpointContractMetadata>()
                : throw new InvalidOperationException("Contract manifest endpoint metadata is required for OpenAPI generation."));

        if (metadata.Count != manifest.Endpoints.Count)
        {
            throw new InvalidOperationException("Contract manifest endpoint metadata must align with the endpoint list.");
        }

        var modelsByName = manifest.Models.ToDictionary(model => model.Name, StringComparer.Ordinal);
        var modelsBySourceType = manifest.Models.ToDictionary(model => model.SourceType, StringComparer.Ordinal);
        var document = BuildDocument(manifest.Endpoints, metadata, modelsByName, modelsBySourceType, title, version);
        var json = Render(document);

        return new OpenApiDocumentResult(json, ComputeSha256(json));
    }

    private static OpenApiDocumentData BuildDocument(
        IReadOnlyList<EndpointManifestEntry> endpoints,
        IReadOnlyList<EndpointContractMetadata> metadata,
        Dictionary<string, ContractModel> modelsByName,
        Dictionary<string, ContractModel> modelsBySourceType,
        string title,
        string version)
    {
        var operations = endpoints
            .Select((endpoint, index) => (endpoint, metadata: metadata[index]))
            .OrderBy(item => item.endpoint.Route, StringComparer.Ordinal)
            .ThenBy(item => item.endpoint.Method, StringComparer.Ordinal)
            .Select(item => new
            {
                item.endpoint.Route,
                Operation = BuildOperation(item.endpoint, item.metadata, modelsByName, modelsBySourceType)
            })
            .GroupBy(item => item.Route, StringComparer.Ordinal)
            .Select(group => new OpenApiPathItem(
                group.Key,
                group.Select(item => item.Operation).ToArray()))
            .ToArray();

        var schemas = modelsByName.Values
            .OrderBy(model => model.Name, StringComparer.Ordinal)
            .Select(model => new OpenApiNamedSchema(model.Name, BuildModelSchema(model, modelsByName)))
            .ToArray();

        return new OpenApiDocumentData(
            RequireValue(title, nameof(title)),
            RequireValue(version, nameof(version)),
            operations,
            schemas);
    }

    private static OpenApiOperation BuildOperation(
        EndpointManifestEntry endpoint,
        EndpointContractMetadata metadata,
        Dictionary<string, ContractModel> modelsByName,
        Dictionary<string, ContractModel> modelsBySourceType)
    {
        var parameters = metadata.PathParameters
            .Select(parameter => BuildPathParameter(parameter, endpoint.Id, modelsByName))
            .Concat(metadata.QueryParameters.Select(parameter => BuildParameter(
                parameter,
                "query",
                required: !parameter.Optional,
                endpoint.Id,
                modelsByName)))
            .ToArray();

        var requestBody = metadata.RequestBody is null
            ? null
            : new OpenApiRequestBody(BuildSchema(
                metadata.RequestBody.Type,
                nullable: false,
                context: endpoint.Id + ".requestBody",
                modelsByName));

        var responseSchema = metadata.SuccessStatusCode == 204
            ? null
            : BuildResponseSchema(endpoint, modelsByName, modelsBySourceType);

        var tags = metadata.Tags.Count > 0
            ? metadata.Tags.ToArray()
            : endpoint.Tags.OrderBy(tag => tag, StringComparer.Ordinal).ToArray();

        return new OpenApiOperation(
            endpoint.Method.ToLowerInvariant(),
            endpoint.Id,
            tags,
            parameters,
            requestBody,
            new OpenApiResponse(metadata.SuccessStatusCode, responseSchema));
    }

    private static OpenApiParameter BuildPathParameter(
        EndpointParameterMetadata parameter,
        string endpointId,
        Dictionary<string, ContractModel> modelsByName)
    {
        if (parameter.Optional)
        {
            throw new InvalidOperationException(
                $"OpenAPI path parameters must be required; endpoint '{endpointId}' declares optional route parameter '{parameter.Name}'.");
        }

        return BuildParameter(parameter, "path", required: true, endpointId, modelsByName);
    }

    private static OpenApiParameter BuildParameter(
        EndpointParameterMetadata parameter,
        string location,
        bool required,
        string endpointId,
        Dictionary<string, ContractModel> modelsByName)
    {
        return new OpenApiParameter(
            parameter.Name,
            location,
            required,
            BuildSchema(
                parameter.Type,
                parameter.Nullable,
                endpointId + "." + location + "." + parameter.Name,
                modelsByName));
    }

    private static OpenApiSchema? BuildResponseSchema(
        EndpointManifestEntry endpoint,
        Dictionary<string, ContractModel> modelsByName,
        Dictionary<string, ContractModel> modelsBySourceType)
    {
        if (string.Equals(endpoint.Response, "void", StringComparison.OrdinalIgnoreCase)
            || string.Equals(endpoint.Response, "System.Void", StringComparison.OrdinalIgnoreCase)
            || string.Equals(endpoint.Response, "global::System.Void", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (modelsBySourceType.TryGetValue(endpoint.Response, out var sourceTypeMatch))
        {
            return OpenApiSchema.Reference(sourceTypeMatch.Name);
        }

        if (modelsByName.TryGetValue(endpoint.Response, out var nameMatch))
        {
            return OpenApiSchema.Reference(nameMatch.Name);
        }

        throw new InvalidOperationException(
            $"OpenAPI generation could not resolve a response model for endpoint '{endpoint.Id}' from '{endpoint.Response}'.");
    }

    private static OpenApiSchema BuildModelSchema(ContractModel model, Dictionary<string, ContractModel> modelsByName)
    {
        if (model.EnumValues.Count > 0)
        {
            return OpenApiSchema.NumericEnum(
                model.EnumValues.Select(value => value.Value).ToArray(),
                model.EnumValues.Select(value => value.Name).ToArray());
        }

        var properties = model.Properties
            .Select(property => new OpenApiProperty(
                property.Name,
                BuildSchema(
                    property.Type,
                    property.Nullable,
                    model.Name + "." + property.Name,
                    modelsByName)))
            .ToArray();

        var required = model.Properties
            .Where(property => !property.Optional)
            .Select(property => property.Name)
            .ToArray();

        return OpenApiSchema.Object(properties, required);
    }

    private static OpenApiSchema BuildSchema(
        ContractTypeDescriptor descriptor,
        bool nullable,
        string context,
        Dictionary<string, ContractModel> modelsByName)
    {
        return descriptor.Kind switch
        {
            ContractTypeKind.String => OpenApiSchema.Primitive("string", format: null, nullable),
            ContractTypeKind.Boolean => OpenApiSchema.Primitive("boolean", format: null, nullable),
            ContractTypeKind.Integer => OpenApiSchema.Primitive("integer", format: null, nullable),
            ContractTypeKind.Decimal => OpenApiSchema.Primitive("number", format: null, nullable),
            ContractTypeKind.Guid => OpenApiSchema.Primitive("string", "uuid", nullable),
            ContractTypeKind.DateTime => OpenApiSchema.Primitive("string", "date-time", nullable),
            ContractTypeKind.DateOnly => OpenApiSchema.Primitive("string", "date", nullable),
            ContractTypeKind.TimeOnly => OpenApiSchema.Primitive("string", "time", nullable),
            ContractTypeKind.Array => OpenApiSchema.Array(
                BuildSchema(
                    descriptor.ElementType ?? throw new InvalidOperationException("Array descriptors require an element type."),
                    nullable: false,
                    context: context + "[]",
                    modelsByName),
                nullable),
            ContractTypeKind.Dictionary => OpenApiSchema.Dictionary(
                BuildSchema(
                    descriptor.ElementType ?? throw new InvalidOperationException("Dictionary descriptors require an element type."),
                    nullable: false,
                    context: context + "{}",
                    modelsByName),
                nullable),
            ContractTypeKind.Object => OpenApiSchema.Reference(
                ResolveModelName(
                    descriptor.ReferenceName,
                    expectedEnum: false,
                    context,
                    modelsByName),
                nullable),
            ContractTypeKind.Enum => OpenApiSchema.Reference(
                ResolveModelName(
                    descriptor.ReferenceName,
                    expectedEnum: true,
                    context,
                    modelsByName),
                nullable),
            ContractTypeKind.Unknown => throw new NotSupportedException(
                $"OpenAPI generation does not support contract type kind '{descriptor.Kind}' for '{context}'."),
            _ => throw new NotSupportedException(
                $"OpenAPI generation does not support contract type kind '{descriptor.Kind}' for '{context}'.")
        };
    }

    private static string ResolveModelName(
        string? referenceName,
        bool expectedEnum,
        string context,
        Dictionary<string, ContractModel> modelsByName)
    {
        if (referenceName is null || !modelsByName.TryGetValue(referenceName, out var model))
        {
            throw new InvalidOperationException(
                $"OpenAPI generation could not resolve referenced model '{referenceName}' for '{context}'.");
        }

        var isEnum = model.EnumValues.Count > 0;
        if (expectedEnum != isEnum)
        {
            throw new InvalidOperationException(
                $"OpenAPI generation expected {(expectedEnum ? "enum" : "object")} model '{referenceName}' for '{context}'.");
        }

        return model.Name;
    }

    private static string Render(OpenApiDocumentData document)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            document.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string ComputeSha256(string json)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static string RequireValue(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty value is required.", parameterName)
            : value;
    }
}

internal sealed record OpenApiDocumentData(
    string Title,
    string Version,
    IReadOnlyList<OpenApiPathItem> Paths,
    IReadOnlyList<OpenApiNamedSchema> Schemas)
{
    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("openapi", "3.1.0");
        writer.WriteString("jsonSchemaDialect", "https://spec.openapis.org/oas/3.1/dialect/base");
        writer.WritePropertyName("info");
        writer.WriteStartObject();
        writer.WriteString("title", Title);
        writer.WriteString("version", Version);
        writer.WriteEndObject();
        writer.WritePropertyName("paths");
        writer.WriteStartObject();

        foreach (var path in Paths)
        {
            writer.WritePropertyName(path.Route);
            path.WriteTo(writer);
        }

        writer.WriteEndObject();
        writer.WritePropertyName("components");
        writer.WriteStartObject();
        writer.WritePropertyName("schemas");
        writer.WriteStartObject();

        foreach (var schema in Schemas)
        {
            writer.WritePropertyName(schema.Name);
            schema.Schema.WriteTo(writer);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}

internal sealed record OpenApiPathItem(string Route, IReadOnlyList<OpenApiOperation> Operations)
{
    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();

        foreach (var operation in Operations)
        {
            writer.WritePropertyName(operation.Method);
            operation.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}

internal sealed record OpenApiOperation(
    string Method,
    string OperationId,
    IReadOnlyList<string> Tags,
    IReadOnlyList<OpenApiParameter> Parameters,
    OpenApiRequestBody? RequestBody,
    OpenApiResponse Response)
{
    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("operationId", OperationId);

        if (Tags.Count > 0)
        {
            writer.WritePropertyName("tags");
            writer.WriteStartArray();

            foreach (var tag in Tags)
            {
                writer.WriteStringValue(tag);
            }

            writer.WriteEndArray();
        }

        if (Parameters.Count > 0)
        {
            writer.WritePropertyName("parameters");
            writer.WriteStartArray();

            foreach (var parameter in Parameters)
            {
                parameter.WriteTo(writer);
            }

            writer.WriteEndArray();
        }

        if (RequestBody is not null)
        {
            writer.WritePropertyName("requestBody");
            RequestBody.WriteTo(writer);
        }

        writer.WritePropertyName("responses");
        writer.WriteStartObject();
        writer.WritePropertyName(Response.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Response.WriteTo(writer);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}

internal sealed record OpenApiParameter(string Name, string Location, bool Required, OpenApiSchema Schema)
{
    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("name", Name);
        writer.WriteString("in", Location);
        writer.WriteBoolean("required", Required);
        writer.WritePropertyName("schema");
        Schema.WriteTo(writer);
        writer.WriteEndObject();
    }
}

internal sealed record OpenApiRequestBody(OpenApiSchema Schema)
{
    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("required", true);
        writer.WritePropertyName("content");
        writer.WriteStartObject();
        writer.WritePropertyName("application/json");
        writer.WriteStartObject();
        writer.WritePropertyName("schema");
        Schema.WriteTo(writer);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}

internal sealed record OpenApiResponse(int StatusCode, OpenApiSchema? Schema)
{
    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("description", "Success");

        if (Schema is not null)
        {
            writer.WritePropertyName("content");
            writer.WriteStartObject();
            writer.WritePropertyName("application/json");
            writer.WriteStartObject();
            writer.WritePropertyName("schema");
            Schema.WriteTo(writer);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }
}

internal sealed record OpenApiNamedSchema(string Name, OpenApiSchema Schema);

internal sealed record OpenApiProperty(string Name, OpenApiSchema Schema);

internal enum OpenApiSchemaKind
{
    Primitive,
    Array,
    Dictionary,
    Reference,
    Object,
    NumericEnum
}

internal sealed record OpenApiSchema(
    OpenApiSchemaKind Kind,
    bool Nullable = false,
    string? Type = null,
    string? Format = null,
    string? ReferenceName = null,
    OpenApiSchema? ElementSchema = null,
    IReadOnlyList<OpenApiProperty>? Properties = null,
    IReadOnlyList<string>? RequiredProperties = null,
    IReadOnlyList<int>? EnumValues = null,
    IReadOnlyList<string>? EnumNames = null)
{
    public static OpenApiSchema Primitive(string type, string? format, bool nullable)
        => new(OpenApiSchemaKind.Primitive, nullable, Type: type, Format: format);

    public static OpenApiSchema Array(OpenApiSchema items, bool nullable)
        => new(OpenApiSchemaKind.Array, nullable, ElementSchema: items);

    public static OpenApiSchema Dictionary(OpenApiSchema values, bool nullable)
        => new(OpenApiSchemaKind.Dictionary, nullable, ElementSchema: values);

    public static OpenApiSchema Reference(string referenceName, bool nullable = false)
        => new(OpenApiSchemaKind.Reference, nullable, ReferenceName: referenceName);

    public static OpenApiSchema Object(IReadOnlyList<OpenApiProperty> properties, IReadOnlyList<string> requiredProperties)
        => new(OpenApiSchemaKind.Object, Properties: properties, RequiredProperties: requiredProperties);

    public static OpenApiSchema NumericEnum(IReadOnlyList<int> values, IReadOnlyList<string> names)
        => new(OpenApiSchemaKind.NumericEnum, EnumValues: values, EnumNames: names);

    public void WriteTo(Utf8JsonWriter writer)
    {
        if (Nullable && Kind != OpenApiSchemaKind.Primitive)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("anyOf");
            writer.WriteStartArray();
            WriteCore(writer);
            writer.WriteStartObject();
            writer.WriteString("type", "null");
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
            return;
        }

        if (Nullable && Kind == OpenApiSchemaKind.Primitive)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("type");
            writer.WriteStartArray();
            writer.WriteStringValue(Type);
            writer.WriteStringValue("null");
            writer.WriteEndArray();

            if (Format is not null)
            {
                writer.WriteString("format", Format);
            }

            writer.WriteEndObject();
            return;
        }

        WriteCore(writer);
    }

    private void WriteCore(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();

        switch (Kind)
        {
            case OpenApiSchemaKind.Primitive:
                writer.WriteString("type", Type);
                if (Format is not null)
                {
                    writer.WriteString("format", Format);
                }

                break;

            case OpenApiSchemaKind.Array:
                writer.WriteString("type", "array");
                writer.WritePropertyName("items");
                ElementSchema!.WriteTo(writer);
                break;

            case OpenApiSchemaKind.Dictionary:
                writer.WriteString("type", "object");
                writer.WritePropertyName("additionalProperties");
                ElementSchema!.WriteTo(writer);
                break;

            case OpenApiSchemaKind.Reference:
                writer.WriteString("$ref", "#/components/schemas/" + ReferenceName);
                break;

            case OpenApiSchemaKind.Object:
                writer.WriteString("type", "object");
                writer.WritePropertyName("properties");
                writer.WriteStartObject();

                foreach (var property in Properties!)
                {
                    writer.WritePropertyName(property.Name);
                    property.Schema.WriteTo(writer);
                }

                writer.WriteEndObject();

                if (RequiredProperties!.Count > 0)
                {
                    writer.WritePropertyName("required");
                    writer.WriteStartArray();

                    foreach (var requiredProperty in RequiredProperties)
                    {
                        writer.WriteStringValue(requiredProperty);
                    }

                    writer.WriteEndArray();
                }

                break;

            case OpenApiSchemaKind.NumericEnum:
                writer.WriteString("type", "integer");
                writer.WritePropertyName("enum");
                writer.WriteStartArray();

                foreach (var value in EnumValues!)
                {
                    writer.WriteNumberValue(value);
                }

                writer.WriteEndArray();
                writer.WritePropertyName("x-enumNames");
                writer.WriteStartArray();

                foreach (var name in EnumNames!)
                {
                    writer.WriteStringValue(name);
                }

                writer.WriteEndArray();
                break;

            default:
                throw new NotSupportedException($"Unsupported OpenAPI schema kind '{Kind}'.");
        }

        writer.WriteEndObject();
    }
}
