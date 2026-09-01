using Dotisan.Core;

namespace Dotisan.TypeScript;

public static class ValidationSchemaGenerator
{
    public static string Apply(ContractManifest manifest, string modelName, ContractProperty property, string schema)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        foreach (var rule in RulesFor(manifest, modelName, property.Name))
        {
            schema = rule.Kind.ToLowerInvariant() switch
            {
                "required" => schema,
                "email" => schema + ".email()",
                "length" => schema + $".min({RequireValue(rule, "length")})",
                "range" => ApplyRange(schema, RequireValue(rule, "range")),
                "pattern" => schema + $".regex(/{RequireValue(rule, "pattern")}/)",
                _ => throw new InvalidOperationException($"Validation rule '{rule.Kind}' is not supported by client-side generation. Keep FluentValidation authoritative and use a supported portable rule.")
            };
        }

        return schema;
    }

    public static bool IsRequired(ContractManifest manifest, string modelName, string propertyName)
        => RulesFor(manifest, modelName, propertyName).Any(rule => rule.Kind.Equals("required", StringComparison.OrdinalIgnoreCase));

    private static EndpointValidationRuleMetadata[] RulesFor(ContractManifest manifest, string modelName, string propertyName)
    {
        var rules = manifest.EndpointMetadata?
            .SelectMany(metadata => metadata.Validation.Rules)
            .Where(rule => MatchesTarget(rule.Target, modelName, propertyName))
            .OrderBy(rule => RuleOrder(rule.Kind))
            .ThenBy(rule => rule.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(rule => rule.Value, StringComparer.Ordinal)
            .ToArray();
        return rules ?? [];
    }

    private static bool MatchesTarget(string target, string modelName, string propertyName)
    {
        var normalized = target.Split('.').Last();
        return normalized.Equals(propertyName, StringComparison.OrdinalIgnoreCase)
            || target.Equals(modelName + "." + propertyName, StringComparison.OrdinalIgnoreCase);
    }

    private static int RuleOrder(string kind) => kind.ToLowerInvariant() switch
    {
        "required" => 0,
        "email" => 1,
        "length" => 2,
        "range" => 3,
        "pattern" => 4,
        _ => 100
    };

    private static string RequireValue(EndpointValidationRuleMetadata rule, string kind)
        => string.IsNullOrWhiteSpace(rule.Value)
            ? throw new InvalidOperationException($"Validation rule '{kind}' for '{rule.Target}' requires a value.")
            : rule.Value;

    private static string ApplyRange(string schema, string value)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        return parts.Length == 2
            ? schema + $".min({parts[0]}).max({parts[1]})"
            : throw new InvalidOperationException("The range validation rule requires a 'min,max' value.");
    }
}
