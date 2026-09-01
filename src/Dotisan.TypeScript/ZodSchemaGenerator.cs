using System.Text;
using Dotisan.Core;

namespace Dotisan.TypeScript;

public static class ZodSchemaGenerator
{
    public static GeneratedTypeScriptFile Generate(ContractManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var builder = new StringBuilder("import { z } from \"zod\";").AppendLine().AppendLine();

        foreach (var model in manifest.Models.OrderBy(model => model.Name, StringComparer.Ordinal))
        {
            if (model.EnumValues.Count > 0)
            {
                var literals = string.Join(", ", model.EnumValues.OrderBy(value => value.Name, StringComparer.Ordinal).Select(value => $"z.literal({value.Value})"));
                builder.Append("export const ").Append(model.Name).Append("Schema = z.union([").Append(literals).AppendLine("]);");
            }
            else
            {
                builder.Append("export const ").Append(model.Name).AppendLine("Schema = z.object({");
                foreach (var property in model.Properties.OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    var isRequired = ValidationSchemaGenerator.IsRequired(manifest, model.Name, property.Name);
                    var propertySchema = ValidationSchemaGenerator.Apply(manifest, model.Name, property, SchemaType(property.Type));
                    builder.Append("  ").Append(property.Name).Append(": ").Append(propertySchema);
                    if (property.Nullable)
                        builder.Append(".nullable()");
                    if (property.Optional && !isRequired)
                        builder.Append(".optional()");
                    builder.AppendLine(",");
                }

                builder.AppendLine("});");
            }

            builder.AppendLine();
        }

        return new GeneratedTypeScriptFile("schemas.ts", builder.ToString());
    }

    private static string SchemaType(ContractTypeDescriptor type)
    {
        return type.Kind switch
        {
            ContractTypeKind.String or ContractTypeKind.Guid or ContractTypeKind.DateTime or ContractTypeKind.DateOnly or ContractTypeKind.TimeOnly => "z.string()",
            ContractTypeKind.Boolean => "z.boolean()",
            ContractTypeKind.Integer or ContractTypeKind.Decimal => "z.number()",
            ContractTypeKind.Array => $"z.array({SchemaType(type.ElementType!)})",
            ContractTypeKind.Dictionary => $"z.record(z.string(), {SchemaType(type.ElementType!)})",
            ContractTypeKind.Object or ContractTypeKind.Enum => $"{type.ReferenceName}Schema",
            ContractTypeKind.Unknown => throw new InvalidOperationException("Cannot generate a Zod schema for an unknown contract type."),
            _ => throw new InvalidOperationException($"Cannot generate a Zod schema for contract type '{type.Kind}'.")
        };
    }
}
