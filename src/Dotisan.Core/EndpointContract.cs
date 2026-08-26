namespace Dotisan.Core;

public sealed class EndpointOptions
{
    public EndpointOptions(
        string id,
        string feature,
        string name,
        string method,
        string route,
        bool authorization = false,
        string? permission = null,
        string? version = null,
        IEnumerable<string>? tags = null,
        bool validation = false,
        string? transaction = null,
        bool idempotency = false,
        string? rateLimit = null,
        bool deprecated = false)
    {
        Id = RequireValue(id, nameof(id));
        Feature = RequireValue(feature, nameof(feature));
        Name = RequireValue(name, nameof(name));
        Method = RequireValue(method, nameof(method)).ToUpperInvariant();
        Route = RequireRoute(route);
        Authorization = authorization;
        Permission = permission;
        Version = version;
        Tags = (tags ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Validation = validation;
        Transaction = transaction;
        Idempotency = idempotency;
        RateLimit = rateLimit;
        Deprecated = deprecated;
    }

    public string Id { get; }
    public string Feature { get; }
    public string Name { get; }
    public string Method { get; }
    public string Route { get; }
    public bool Authorization { get; }
    public string? Permission { get; }
    public string? Version { get; }
    public IReadOnlyList<string> Tags { get; }
    public bool Validation { get; }
    public string? Transaction { get; }
    public bool Idempotency { get; }
    public string? RateLimit { get; }
    public bool Deprecated { get; }

    public EndpointManifestEntry ToManifestEntry(string request, string response)
    {
        return new EndpointManifestEntry(
            Id,
            Feature,
            Name,
            Method,
            Route,
            RequireValue(request, nameof(request)),
            RequireValue(response, nameof(response)),
            Authorization,
            Permission,
            Version,
            Tags,
            Validation,
            Deprecated,
            Transaction,
            Idempotency,
            RateLimit);
    }

    private static string RequireValue(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty value is required.", parameterName)
            : value.Trim();
    }

    private static string RequireRoute(string route)
    {
        var value = RequireValue(route, nameof(route));
        return value.StartsWith('/')
            ? value
            : throw new ArgumentException("Endpoint routes must start with '/'.", nameof(route));
    }
}

public interface IDotisanEndpoint
{
    static abstract EndpointOptions Configure();
}

public interface IDotisanHandler<in TRequest, TResponse>
{
    ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

public interface IDotisanValidator<in TRequest>
{
    ValueTask<ValidationResult> ValidateAsync(TRequest request, CancellationToken cancellationToken);
}

public sealed record ValidationResult(IReadOnlyDictionary<string, string[]> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
