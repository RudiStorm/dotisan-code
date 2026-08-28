using System.Text;
using Dotisan.Core;

namespace Dotisan.TypeScript;

public sealed record GeneratedTypeScriptFile(string Path, string Content);

public static class TypeScriptContractGenerator
{
    public static IReadOnlyList<GeneratedTypeScriptFile> Generate(ContractManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var models = manifest.Models.OrderBy(model => model.Name, StringComparer.Ordinal).ToArray();
        var builder = new StringBuilder();
        var hasWrittenSection = false;

        foreach (var model in models.Where(model => model.EnumValues.Count > 0))
        {
            if (hasWrittenSection)
            {
                builder.AppendLine();
            }

            AppendEnum(builder, model);
            hasWrittenSection = true;
        }

        foreach (var model in models.Where(model => model.EnumValues.Count == 0))
        {
            if (hasWrittenSection)
            {
                builder.AppendLine();
            }

            AppendInterface(builder, model);
            hasWrittenSection = true;
        }

        if (builder.Length == 0)
        {
            builder.AppendLine();
        }

        return [new GeneratedTypeScriptFile("models.ts", builder.ToString())];
    }

    private static void AppendEnum(StringBuilder builder, ContractModel model)
    {
        builder.Append("export enum ").Append(model.Name).AppendLine(" {");

        foreach (var value in model.EnumValues)
        {
            builder.Append("  ").Append(value.Name).Append(" = ").Append(value.Value).AppendLine(",");
        }

        builder.AppendLine("}");
    }

    private static void AppendInterface(StringBuilder builder, ContractModel model)
    {
        builder.Append("export interface ").Append(model.Name).AppendLine(" {");

        foreach (var property in model.Properties.OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            var mappedType = TypeScriptTypeMapper.Map(property.Type, property.Nullable, property.Optional);
            builder.Append("  ").Append(property.Name).Append(": ").Append(mappedType).AppendLine(";");
        }

        builder.AppendLine("}");
    }
}
