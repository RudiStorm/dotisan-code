using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace Dotisan.Core;

public sealed record EndpointManifestEntry(
    string Id,
    string Feature,
    string Name,
    string Method,
    string Route,
    string Request,
    string Response,
    bool Authorization,
    string? Permission,
    string? Version,
    IReadOnlyList<string> Tags,
    bool Validation,
    bool Deprecated,
    string? Transaction = null,
    bool Idempotency = false,
    string? RateLimit = null);


public sealed class EndpointManifest
{
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    public EndpointManifest(IReadOnlyList<EndpointManifestEntry> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        Endpoints = endpoints
            .OrderBy(endpoint => endpoint.Id, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.Method, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.Route, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<EndpointManifestEntry> Endpoints { get; }

    public string ToJson() => JsonSerializer.Serialize(Endpoints, JsonOptions);

    public string Sha256 => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ToJson()))).ToLowerInvariant();
}
