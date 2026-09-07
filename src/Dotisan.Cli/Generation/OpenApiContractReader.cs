using System.Text.Json;
using Dotisan.Core;

namespace Dotisan.Cli.Generation;

public static class OpenApiContractReader
{
    public static ContractManifest Read(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("openapi", out var version) || !version.GetString()!.StartsWith("3.", StringComparison.Ordinal))
            throw new JsonException("The OpenAPI document must use version 3.x.");

        var schemas = ReadSchemas(root);
        var endpoints = new List<EndpointManifestEntry>();
        var metadata = new List<EndpointContractMetadata>();
        foreach (var path in root.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject().Where(item => IsMethod(item.Name)))
            {
                var operationId = operation.Value.TryGetProperty("operationId", out var id) ? id.GetString() : null;
                if (string.IsNullOrWhiteSpace(operationId))
                    throw new JsonException($"OpenAPI operation at '{path.Name}' is missing operationId.");
                if (endpoints.Any(endpoint => endpoint.Id.Equals(operationId, StringComparison.Ordinal)))
                    throw new JsonException($"Duplicate OpenAPI operationId '{operationId}'.");

                var parameters = operation.Value.TryGetProperty("parameters", out var parameterArray)
                    ? parameterArray.EnumerateArray().Select(parameter => ReadParameter(parameter, schemas)).ToArray()
                    : [];
                var pathParameters = parameters.Where(parameter => parameter.Location == "path").Select(parameter => parameter.Metadata).ToArray();
                var queryParameters = parameters.Where(parameter => parameter.Location == "query").Select(parameter => parameter.Metadata).ToArray();
                var request = operation.Value.TryGetProperty("requestBody", out var requestBody)
                    ? ReadContentSchema(requestBody, schemas)
                    : ("void", (ContractTypeDescriptor?)null, false);
                var response = ReadResponse(operation.Value, schemas);
                var tags = operation.Value.TryGetProperty("tags", out var tagArray) ? tagArray.EnumerateArray().Select(tag => tag.GetString()!).ToArray() : [];
                var method = operation.Name.ToUpperInvariant();
                endpoints.Add(new EndpointManifestEntry(operationId, tags.FirstOrDefault() ?? "Api", operationId, method, path.Name,
                    request.Item1, response.Item1, false, null, null, tags, false, false));
                metadata.Add(new EndpointContractMetadata(request.Item2 is null ? null : new EndpointRequestBodyMetadata(request.Item2), pathParameters, queryParameters,
                    response.Item2, tags, new EndpointValidationMetadata(false, [])));
            }
        }

        return new ContractManifest(1, endpoints, schemas.Values.ToArray(), metadata);
    }

    private static Dictionary<string, ContractModel> ReadSchemas(JsonElement root)
    {
        var result = new Dictionary<string, ContractModel>(StringComparer.Ordinal);
        if (!root.TryGetProperty("components", out var components) || !components.TryGetProperty("schemas", out var schemas))
            return result;
        foreach (var schema in schemas.EnumerateObject())
        {
            var value = schema.Value;
            if (value.TryGetProperty("enum", out var values))
            {
                var names = value.TryGetProperty("x-enumNames", out var enumNames) ? enumNames.EnumerateArray().Select(item => item.GetString() ?? "Value").ToArray() : values.EnumerateArray().Select((_, index) => "Value" + index).ToArray();
                result[schema.Name] = new ContractModel(schema.Name, "global::" + schema.Name, [], values.EnumerateArray().Select((item, index) => new ContractEnumValue(names[index], item.GetInt32())).ToArray());
                continue;
            }
            var properties = value.TryGetProperty("properties", out var propertyObject)
                ? propertyObject.EnumerateObject().Select(property => { var parsed = ReadSchema(property.Value, result); var optional = !value.TryGetProperty("required", out var required) || !required.EnumerateArray().Any(item => item.GetString() == property.Name); return new ContractProperty(property.Name, parsed.Descriptor, parsed.Nullable, optional); }).ToArray()
                : [];
            result[schema.Name] = new ContractModel(schema.Name, "global::" + schema.Name, properties, []);
        }
        return result;
    }

    private static (string Type, ContractTypeDescriptor? Descriptor, bool Nullable) ReadContentSchema(JsonElement body, Dictionary<string, ContractModel> schemas)
    {
        var schema = body.GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var parsed = ReadSchema(schema, schemas);
        return (parsed.Descriptor?.ReferenceName?.Replace("global::", string.Empty, StringComparison.Ordinal) ?? "void", parsed.Descriptor, parsed.Nullable);
    }

    private static (string Type, int Status) ReadResponse(JsonElement operation, Dictionary<string, ContractModel> schemas)
    {
        var response = operation.GetProperty("responses").EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal).First();
        var status = int.TryParse(response.Name, out var code) ? code : 200;
        if (!response.Value.TryGetProperty("content", out var content)) return ("void", status);
        var schema = content.GetProperty("application/json").GetProperty("schema");
        var parsed = ReadSchema(schema, schemas);
        return (parsed.Descriptor?.ReferenceName?.Replace("global::", string.Empty, StringComparison.Ordinal) ?? "void", status);
    }

    private static (string Location, EndpointParameterMetadata Metadata) ReadParameter(JsonElement parameter, Dictionary<string, ContractModel> schemas)
    {
        var parsed = ReadSchema(parameter.GetProperty("schema"), schemas);
        return (parameter.GetProperty("in").GetString()!, new EndpointParameterMetadata(parameter.GetProperty("name").GetString()!, parsed.Descriptor!, parsed.Nullable, !parameter.GetProperty("required").GetBoolean()));
    }

    private static (ContractTypeDescriptor Descriptor, bool Nullable) ReadSchema(JsonElement schema, Dictionary<string, ContractModel> schemas)
    {
        if (schema.TryGetProperty("$ref", out var reference)) return (new ContractTypeDescriptor(ContractTypeKind.Object, "global::" + reference.GetString()!.Split('/').Last()), false);
        var nullable = schema.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.Array && type.EnumerateArray().Any(item => item.GetString() == "null");
        var typeName = type.ValueKind == JsonValueKind.Array ? type.EnumerateArray().First(item => item.GetString() != "null").GetString() : type.GetString();
        return typeName switch
        {
            "string" when schema.TryGetProperty("format", out var format) && format.GetString() == "uuid" => (new ContractTypeDescriptor(ContractTypeKind.Guid), nullable),
            "string" => (new ContractTypeDescriptor(ContractTypeKind.String), nullable),
            "boolean" => (new ContractTypeDescriptor(ContractTypeKind.Boolean), nullable),
            "integer" => (new ContractTypeDescriptor(ContractTypeKind.Integer), nullable),
            "number" => (new ContractTypeDescriptor(ContractTypeKind.Decimal), nullable),
            "array" => (new ContractTypeDescriptor(ContractTypeKind.Array, ElementType: ReadSchema(schema.GetProperty("items"), schemas).Descriptor), nullable),
            "object" when schema.TryGetProperty("additionalProperties", out var values) => (new ContractTypeDescriptor(ContractTypeKind.Dictionary, ElementType: ReadSchema(values, schemas).Descriptor), nullable),
            _ => (new ContractTypeDescriptor(ContractTypeKind.Unknown), nullable)
        };
    }

    private static bool IsMethod(string name) => name is "get" or "post" or "put" or "patch" or "delete" or "options" or "head" or "trace";
}
