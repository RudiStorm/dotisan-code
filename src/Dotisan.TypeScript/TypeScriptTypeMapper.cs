using Dotisan.Core;

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
        => Map(ToDescriptor(type), nullable, optional);

    public static string Map(ContractTypeDescriptor type, bool nullable = false, bool optional = false)
    {
        ArgumentNullException.ThrowIfNull(type);

        var mapped = type.Kind switch
        {
            ContractTypeKind.String => "string",
            ContractTypeKind.Boolean => "boolean",
            ContractTypeKind.Integer or ContractTypeKind.Decimal => "number",
            ContractTypeKind.Guid => "string",
            ContractTypeKind.DateTime or ContractTypeKind.DateOnly or ContractTypeKind.TimeOnly => "string",
            ContractTypeKind.Array => $"{Map(type.ElementType!)}[]",
            ContractTypeKind.Dictionary => $"Record<string, {Map(type.ElementType!)}>",
            ContractTypeKind.Object or ContractTypeKind.Enum => type.ReferenceName!,
            _ => "unknown"
        };

        if (nullable)
        {
            mapped += " | null";
        }

        if (optional)
        {
            mapped += " | undefined";
        }

        return mapped;
    }

    private static ContractTypeDescriptor ToDescriptor(ContractType type)
    {
        return type switch
        {
            ContractType.String => new ContractTypeDescriptor(ContractTypeKind.String),
            ContractType.Boolean => new ContractTypeDescriptor(ContractTypeKind.Boolean),
            ContractType.Integer => new ContractTypeDescriptor(ContractTypeKind.Integer),
            ContractType.Decimal => new ContractTypeDescriptor(ContractTypeKind.Decimal),
            ContractType.Guid => new ContractTypeDescriptor(ContractTypeKind.Guid),
            ContractType.DateTime => new ContractTypeDescriptor(ContractTypeKind.DateTime),
            ContractType.DateOnly => new ContractTypeDescriptor(ContractTypeKind.DateOnly),
            ContractType.TimeOnly => new ContractTypeDescriptor(ContractTypeKind.TimeOnly),
            _ => new ContractTypeDescriptor(ContractTypeKind.Unknown)
        };
    }
}
