using System.Text;
using Dotisan.Core;

namespace Dotisan.TypeScript;

public static class TanStackQueryGenerator
{
    public static GeneratedTypeScriptFile Generate(ContractManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var builder = new StringBuilder("import { useMutation, useQuery } from \"@tanstack/vue-query\";\nimport * as services from \"./services\";\n\n");
        foreach (var endpoint in manifest.Endpoints.OrderBy(endpoint => endpoint.Id, StringComparer.Ordinal).ThenBy(endpoint => endpoint.Method, StringComparer.Ordinal))
        {
            var functionName = ToCamelCase(endpoint.Name);
            var hookName = "use" + char.ToUpperInvariant(functionName[0]) + functionName[1..];
            if (endpoint.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                builder.Append("export function ").Append(hookName).AppendLine("Query(...args: Parameters<typeof services." + functionName + ">) {");
                builder.Append("  return useQuery({ queryKey: [\"").Append(endpoint.Id).AppendLine("\", ...args], queryFn: () => services." + functionName + "(...args) });");
                builder.AppendLine("}").AppendLine();
            }
            else
            {
                builder.Append("export function ").Append(hookName).AppendLine("Mutation() {");
                // Forward the complete generated service signature. This keeps route parameters,
                // query options, request bodies, and RequestInit options usable from mutations.
                builder.Append("  return useMutation({ mutationFn: (args: Parameters<typeof services.")
                    .Append(functionName).Append(">) => services.").Append(functionName).AppendLine("(...args) });");
                builder.AppendLine("}").AppendLine();
            }
        }

        return new GeneratedTypeScriptFile("queries.ts", builder.ToString());
    }

    private static string ToCamelCase(string value) => string.IsNullOrEmpty(value) ? "request" : char.ToLowerInvariant(value[0]) + value[1..];
    private static string NormalizeType(string type) => string.IsNullOrWhiteSpace(type) || type == "System.Void" ? "void" : type.Replace("global::", string.Empty, StringComparison.Ordinal);
}
