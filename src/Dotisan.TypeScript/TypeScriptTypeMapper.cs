namespace Dotisan.TypeScript;

public enum ContractType
{
    String,
    Boolean,
    Integer,
    Decimal,
    Guid,
    DateTime,
    DateOnly,
    TimeOnly,
    Unknown
}

public static class TypeScriptTypeMapper
{
    public static string Map(ContractType type, bool nullable = false, bool optional = false)
    {
        var mapped = type switch
        {
            ContractType.String => "string",
            ContractType.Boolean => "boolean",
            ContractType.Integer or ContractType.Decimal => "number",
            ContractType.Guid => "string",
            ContractType.DateTime or ContractType.DateOnly or ContractType.TimeOnly => "string",
            _ => "unknown"
        };

        if (nullable)
        {
            mapped += " | null";
        }

        return optional ? $"{mapped} | undefined" : mapped;
    }
}
