using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dotisan.Core;

public sealed record ContractManifest
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public ContractManifest(
        int SchemaVersion,
        IReadOnlyList<EndpointManifestEntry> Endpoints,
        IReadOnlyList<ContractModel> Models)
        : this(SchemaVersion, Endpoints, Models, EndpointMetadata: null)
    {
    }

    public ContractManifest(
        int SchemaVersion,
        IReadOnlyList<EndpointManifestEntry> Endpoints,
        IReadOnlyList<ContractModel> Models,
        IReadOnlyList<EndpointContractMetadata>? EndpointMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(Endpoints);
        ArgumentNullException.ThrowIfNull(Models);

        if (SchemaVersion != 1)
        {
            throw new ArgumentOutOfRangeException(nameof(SchemaVersion), SchemaVersion, "Contract manifests only support schema version 1.");
        }

        this.SchemaVersion = SchemaVersion;
        if (EndpointMetadata is null)
        {
            this.Endpoints = Endpoints
                .OrderBy(endpoint => endpoint.Id, StringComparer.Ordinal)
                .ThenBy(endpoint => endpoint.Method, StringComparer.Ordinal)
                .ThenBy(endpoint => endpoint.Route, StringComparer.Ordinal)
                .ToArray();
            this.EndpointMetadata = null;
        }
        else
        {
            if (EndpointMetadata.Count != Endpoints.Count)
            {
                throw new ArgumentException("Endpoint metadata must align with the endpoint list.", nameof(EndpointMetadata));
            }

            var orderedEndpoints = Endpoints
                .Select((endpoint, index) => (Endpoint: endpoint, Metadata: EndpointMetadata[index]))
                .OrderBy(tuple => tuple.Endpoint.Id, StringComparer.Ordinal)
                .ThenBy(tuple => tuple.Endpoint.Method, StringComparer.Ordinal)
                .ThenBy(tuple => tuple.Endpoint.Route, StringComparer.Ordinal)
                .ToArray();

            this.Endpoints = orderedEndpoints
                .Select(tuple => tuple.Endpoint)
                .ToArray();
            this.EndpointMetadata = orderedEndpoints
                .Select(tuple => tuple.Metadata)
                .ToArray();
        }

        this.Models = Models
            .OrderBy(model => model.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public int SchemaVersion { get; }

    public IReadOnlyList<EndpointManifestEntry> Endpoints { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<EndpointContractMetadata>? EndpointMetadata { get; }

    public IReadOnlyList<ContractModel> Models { get; }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    [JsonIgnore]
    public string Sha256 => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ToJson()))).ToLowerInvariant();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            WriteIndented = false
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static string RequireValue(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty value is required.", parameterName)
            : value;
    }
}

public sealed record EndpointContractMetadata
{
    public EndpointContractMetadata(
        EndpointRequestBodyMetadata? RequestBody,
        IReadOnlyList<EndpointParameterMetadata> PathParameters,
        IReadOnlyList<EndpointParameterMetadata> QueryParameters,
        int SuccessStatusCode,
        IReadOnlyList<string> Tags,
        EndpointValidationMetadata Validation)
    {
        if (SuccessStatusCode is < 100 or > 999)
        {
            throw new ArgumentOutOfRangeException(nameof(SuccessStatusCode), SuccessStatusCode, "Endpoint success status codes must be valid HTTP status codes.");
        }

        this.RequestBody = RequestBody;
        this.PathParameters = (PathParameters ?? throw new ArgumentNullException(nameof(PathParameters))).ToArray();
        this.QueryParameters = (QueryParameters ?? throw new ArgumentNullException(nameof(QueryParameters)))
            .OrderBy(parameter => parameter.Name, StringComparer.Ordinal)
            .ToArray();
        this.SuccessStatusCode = SuccessStatusCode;
        this.Tags = (Tags ?? throw new ArgumentNullException(nameof(Tags)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();
        this.Validation = Validation ?? throw new ArgumentNullException(nameof(Validation));
    }

    public EndpointRequestBodyMetadata? RequestBody { get; }

    public IReadOnlyList<EndpointParameterMetadata> PathParameters { get; }

    public IReadOnlyList<EndpointParameterMetadata> QueryParameters { get; }

    public int SuccessStatusCode { get; }

    public IReadOnlyList<string> Tags { get; }

    public EndpointValidationMetadata Validation { get; }

    public static EndpointContractMetadata Create(
        string method,
        string route,
        EndpointRequestBodyMetadata? requestBody,
        IReadOnlyList<EndpointParameterMetadata>? requestParameters,
        IReadOnlyList<string>? tags,
        bool validation)
    {
        var normalizedMethod = RequireValue(method, nameof(method)).ToUpperInvariant();
        var normalizedRoute = RequireRoute(route, nameof(route));
        var parameters = (requestParameters ?? []).ToArray();
        var routeTokens = ParseRouteTokens(normalizedRoute);
        var pathParametersByName = parameters
            .GroupBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var pathParameters = routeTokens
            .Select(token => CreatePathParameter(token, pathParametersByName))
            .ToArray();
        var pathParameterNames = routeTokens
            .Select(token => token.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var queryParameters = UsesRequestBody(normalizedMethod)
            ? []
            : parameters
                .Where(parameter => !pathParameterNames.Contains(parameter.Name))
                .ToArray();

        return new EndpointContractMetadata(
            UsesRequestBody(normalizedMethod) ? requestBody : null,
            pathParameters,
            queryParameters,
            InferSuccessStatusCode(normalizedMethod),
            tags ?? [],
            new EndpointValidationMetadata(validation, []));
    }

    public static int InferSuccessStatusCode(string method)
    {
        return RequireValue(method, nameof(method)).ToUpperInvariant() switch
        {
            "POST" => 201,
            "DELETE" => 204,
            _ => 200
        };
    }

    public static bool UsesRequestBody(string method)
    {
        return RequireValue(method, nameof(method)).ToUpperInvariant() is "POST" or "PUT" or "PATCH";
    }

    private static EndpointParameterMetadata CreatePathParameter(
        RouteToken token,
        Dictionary<string, EndpointParameterMetadata> requestParametersByName)
    {
        if (requestParametersByName.TryGetValue(token.Name, out var matchingParameter))
        {
            return new EndpointParameterMetadata(
                token.Name,
                matchingParameter.Type,
                matchingParameter.Nullable,
                token.Optional || matchingParameter.Optional);
        }

        return new EndpointParameterMetadata(
            token.Name,
            InferRouteParameterType(token.Constraint),
            false,
            token.Optional);
    }

    private static ContractTypeDescriptor InferRouteParameterType(string? constraint)
    {
        return string.Equals(constraint, "guid", StringComparison.OrdinalIgnoreCase)
            ? new ContractTypeDescriptor(ContractTypeKind.Guid)
            : string.Equals(constraint, "bool", StringComparison.OrdinalIgnoreCase)
                ? new ContractTypeDescriptor(ContractTypeKind.Boolean)
                : string.Equals(constraint, "datetime", StringComparison.OrdinalIgnoreCase)
                    ? new ContractTypeDescriptor(ContractTypeKind.DateTime)
                    : string.Equals(constraint, "decimal", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(constraint, "double", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(constraint, "float", StringComparison.OrdinalIgnoreCase)
                            ? new ContractTypeDescriptor(ContractTypeKind.Decimal)
                            : string.Equals(constraint, "byte", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(constraint, "short", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(constraint, "int", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(constraint, "long", StringComparison.OrdinalIgnoreCase)
                                    ? new ContractTypeDescriptor(ContractTypeKind.Integer)
                                    : new ContractTypeDescriptor(ContractTypeKind.String);
    }

    private static RouteToken[] ParseRouteTokens(string route)
    {
        var tokens = new List<RouteToken>();

        for (var index = 0; index < route.Length; index++)
        {
            if (route[index] != '{')
            {
                continue;
            }

            var end = route.IndexOf('}', index + 1);
            if (end < 0)
            {
                break;
            }

            var tokenContent = route.Substring(index + 1, end - index - 1);
            if (TryParseRouteToken(tokenContent, out var token))
            {
                tokens.Add(token);
            }

            index = end;
        }

        return tokens.ToArray();
    }

    private static bool TryParseRouteToken(string tokenContent, out RouteToken token)
    {
        var content = tokenContent.Trim();
        while (content.StartsWith('*'))
        {
            content = content.Substring(1);
        }

        var optional = content.EndsWith('?');
        if (optional)
        {
            content = content[..^1];
        }

        var defaultSeparator = content.IndexOf('=');
        if (defaultSeparator >= 0)
        {
            content = content[..defaultSeparator];
        }

        var segments = content.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0 || string.IsNullOrWhiteSpace(segments[0]))
        {
            token = default;
            return false;
        }

        string? constraint = null;
        for (var index = 1; index < segments.Length; index++)
        {
            var candidate = StripConstraintArguments(segments[index]);
            if (IsTypedRouteConstraint(candidate))
            {
                constraint = candidate;
                break;
            }
        }

        token = new RouteToken(segments[0], constraint, optional);
        return true;
    }

    private static bool IsTypedRouteConstraint(string constraint)
    {
        return string.Equals(constraint, "bool", StringComparison.OrdinalIgnoreCase)
            || string.Equals(constraint, "byte", StringComparison.OrdinalIgnoreCase)
            || string.Equals(constraint, "datetime", StringComparison.OrdinalIgnoreCase)
            || string.Equals(constraint, "decimal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(constraint, "double", StringComparison.OrdinalIgnoreCase)
            || string.Equals(constraint, "float", StringComparison.OrdinalIgnoreCase)
            || string.Equals(constraint, "guid", StringComparison.OrdinalIgnoreCase)
            || string.Equals(constraint, "int", StringComparison.OrdinalIgnoreCase)
            || string.Equals(constraint, "long", StringComparison.OrdinalIgnoreCase)
            || string.Equals(constraint, "short", StringComparison.OrdinalIgnoreCase);
    }

    private static string StripConstraintArguments(string constraint)
    {
        var separator = constraint.IndexOf('(');
        return separator >= 0
            ? constraint[..separator]
            : constraint;
    }

    private static string RequireValue(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty value is required.", parameterName)
            : value;
    }

    private static string RequireRoute(string route, string parameterName)
    {
        var value = RequireValue(route, parameterName);
        return value.StartsWith('/')
            ? value
            : throw new ArgumentException("Endpoint routes must start with '/'.", parameterName);
    }

    private readonly record struct RouteToken(string Name, string? Constraint, bool Optional);
}

public sealed record EndpointRequestBodyMetadata
{
    public EndpointRequestBodyMetadata(ContractTypeDescriptor Type)
    {
        this.Type = Type ?? throw new ArgumentNullException(nameof(Type));
    }

    public ContractTypeDescriptor Type { get; }
}

public sealed record EndpointParameterMetadata
{
    public EndpointParameterMetadata(string Name, ContractTypeDescriptor Type, bool Nullable, bool Optional)
    {
        this.Name = RequireValue(Name, nameof(Name));
        this.Type = Type ?? throw new ArgumentNullException(nameof(Type));
        this.Nullable = Nullable;
        this.Optional = Optional;
    }

    public string Name { get; }

    public ContractTypeDescriptor Type { get; }

    public bool Nullable { get; }

    public bool Optional { get; }

    private static string RequireValue(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty value is required.", parameterName)
            : value;
    }
}

public sealed record EndpointValidationMetadata
{
    public EndpointValidationMetadata(bool Enabled, IReadOnlyList<EndpointValidationRuleMetadata> Rules)
    {
        this.Enabled = Enabled;
        this.Rules = (Rules ?? throw new ArgumentNullException(nameof(Rules)))
            .OrderBy(rule => rule.Target, StringComparer.Ordinal)
            .ThenBy(rule => rule.Kind, StringComparer.Ordinal)
            .ToArray();
    }

    public bool Enabled { get; }

    public IReadOnlyList<EndpointValidationRuleMetadata> Rules { get; }
}

public sealed record EndpointValidationRuleMetadata
{
    public EndpointValidationRuleMetadata(string Target, string Kind)
    {
        this.Target = RequireValue(Target, nameof(Target));
        this.Kind = RequireValue(Kind, nameof(Kind));
    }

    public string Target { get; }

    public string Kind { get; }

    private static string RequireValue(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty value is required.", parameterName)
            : value;
    }
}

public sealed record ContractModel
{
    public ContractModel(
        string Name,
        string SourceType,
        IReadOnlyList<ContractProperty> Properties,
        IReadOnlyList<ContractEnumValue> EnumValues)
    {
        this.Name = RequireValue(Name, nameof(Name));
        this.SourceType = RequireValue(SourceType, nameof(SourceType));
        this.Properties = (Properties ?? throw new ArgumentNullException(nameof(Properties)))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();
        this.EnumValues = (EnumValues ?? throw new ArgumentNullException(nameof(EnumValues)))
            .OrderBy(enumValue => enumValue.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public string Name { get; }

    public string SourceType { get; }

    public IReadOnlyList<ContractProperty> Properties { get; }

    public IReadOnlyList<ContractEnumValue> EnumValues { get; }

    private static string RequireValue(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty value is required.", parameterName)
            : value;
    }
}

public sealed record ContractProperty
{
    public ContractProperty(string Name, ContractTypeDescriptor Type, bool Nullable, bool Optional)
    {
        this.Name = RequireValue(Name, nameof(Name));
        this.Type = Type ?? throw new ArgumentNullException(nameof(Type));
        this.Nullable = Nullable;
        this.Optional = Optional;
    }

    public string Name { get; }

    public ContractTypeDescriptor Type { get; }

    public bool Nullable { get; }

    public bool Optional { get; }

    private static string RequireValue(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty value is required.", parameterName)
            : value;
    }
}

public sealed record ContractEnumValue
{
    public ContractEnumValue(string Name, int Value)
    {
        this.Name = RequireValue(Name, nameof(Name));
        this.Value = Value;
    }

    public string Name { get; }

    public int Value { get; }

    private static string RequireValue(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty value is required.", parameterName)
            : value;
    }
}

#pragma warning disable CA1720
public enum ContractTypeKind
{
    String,
    Boolean,
    Integer,
    Decimal,
    Guid,
    DateTime,
    DateOnly,
    TimeOnly,
    Array,
    Dictionary,
    Object,
    Enum,
    Unknown
}
#pragma warning restore CA1720

public sealed record ContractTypeDescriptor
{
    public ContractTypeDescriptor(
        ContractTypeKind Kind,
        string? ReferenceName = null,
        ContractTypeDescriptor? ElementType = null)
    {
        this.Kind = Kind;
        this.ReferenceName = ReferenceName is null ? null : RequireValue(ReferenceName, nameof(ReferenceName));
        this.ElementType = ElementType;
        Validate();
    }

    public ContractTypeKind Kind { get; }

    public string? ReferenceName { get; }

    public ContractTypeDescriptor? ElementType { get; }

    private void Validate()
    {
        var hasReferenceName = ReferenceName is not null;
        var hasElementType = ElementType is not null;

        if (Kind is ContractTypeKind.Object or ContractTypeKind.Enum)
        {
            if (!hasReferenceName)
            {
                throw new ArgumentException("Object and enum descriptors require a reference name.", nameof(ReferenceName));
            }

            if (hasElementType)
            {
                throw new ArgumentException("Object and enum descriptors cannot declare an element type.", nameof(ElementType));
            }

            return;
        }

        if (Kind is ContractTypeKind.Array or ContractTypeKind.Dictionary)
        {
            if (hasReferenceName)
            {
                throw new ArgumentException("Array and dictionary descriptors cannot declare a reference name.", nameof(ReferenceName));
            }

            if (!hasElementType)
            {
                throw new ArgumentException("Array and dictionary descriptors require an element type.", nameof(ElementType));
            }

            return;
        }

        if (hasReferenceName)
        {
            throw new ArgumentException("Primitive and unknown descriptors cannot declare a reference name.", nameof(ReferenceName));
        }

        if (hasElementType)
        {
            throw new ArgumentException("Primitive and unknown descriptors cannot declare an element type.", nameof(ElementType));
        }
    }

    private static string RequireValue(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty value is required.", parameterName)
            : value;
    }
}
