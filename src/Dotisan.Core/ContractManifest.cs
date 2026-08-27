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
    {
        ArgumentNullException.ThrowIfNull(Endpoints);
        ArgumentNullException.ThrowIfNull(Models);

        this.SchemaVersion = SchemaVersion;
        this.Endpoints = Endpoints
            .OrderBy(endpoint => endpoint.Id, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.Method, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.Route, StringComparer.Ordinal)
            .ToArray();
        this.Models = Models
            .OrderBy(model => model.Name, StringComparer.Ordinal)
            .Select(NormalizeModel)
            .ToArray();
    }

    public int SchemaVersion { get; }

    public IReadOnlyList<EndpointManifestEntry> Endpoints { get; }

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

    private static ContractModel NormalizeModel(ContractModel model)
    {
        return new ContractModel(
            RequireValue(model.Name, nameof(model.Name)),
            RequireValue(model.SourceType, nameof(model.SourceType)),
            model.Properties
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(NormalizeProperty)
                .ToArray(),
            model.EnumValues
                .OrderBy(enumValue => enumValue.Name, StringComparer.Ordinal)
                .Select(NormalizeEnumValue)
                .ToArray());
    }

    private static ContractProperty NormalizeProperty(ContractProperty property)
    {
        return new ContractProperty(
            RequireValue(property.Name, nameof(property.Name)),
            NormalizeDescriptor(property.Type),
            property.Nullable,
            property.Optional);
    }

    private static ContractEnumValue NormalizeEnumValue(ContractEnumValue enumValue)
    {
        return new ContractEnumValue(
            RequireValue(enumValue.Name, nameof(enumValue.Name)),
            enumValue.Value);
    }

    private static ContractTypeDescriptor NormalizeDescriptor(ContractTypeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.Normalize();
    }

    private static string RequireValue(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty value is required.", parameterName)
            : value.Trim();
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
        this.Properties = Properties ?? throw new ArgumentNullException(nameof(Properties));
        this.EnumValues = EnumValues ?? throw new ArgumentNullException(nameof(EnumValues));
    }

    public string Name { get; }

    public string SourceType { get; }

    public IReadOnlyList<ContractProperty> Properties { get; }

    public IReadOnlyList<ContractEnumValue> EnumValues { get; }

    private static string RequireValue(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty value is required.", parameterName)
            : value.Trim();
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
            : value.Trim();
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
            : value.Trim();
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

    internal ContractTypeDescriptor Normalize() => this;

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
            : value.Trim();
    }
}
