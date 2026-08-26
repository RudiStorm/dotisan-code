using System.Text.Json;
using System.Text.Json.Serialization;

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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    public EndpointManifest(IReadOnlyList<EndpointManifestEntry> endpoints)
    {
        Endpoints = endpoints ?? throw new ArgumentNullException(nameof(endpoints));
    }

    public IReadOnlyList<EndpointManifestEntry> Endpoints { get; }

    public string ToJson() => JsonSerializer.Serialize(Endpoints, JsonOptions);
}
