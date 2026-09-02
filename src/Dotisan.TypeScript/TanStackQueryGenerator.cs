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
                var requestType = NormalizeType(endpoint.Request);
                builder.Append("export function ").Append(hookName).AppendLine("Mutation() {");
                if (UsesBody(endpoint.Method) && requestType != "void")
                    builder.Append("  return useMutation({ mutationFn: (body: Parameters<typeof services.").Append(functionName).AppendLine(">[0]) => services." + functionName + "(body) });");
                else if (endpoint.Route.Contains('{'))
                    builder.Append("  return useMutation({ mutationFn: (value: Parameters<typeof services.").Append(functionName).Append(">[0]) => services.").Append(functionName).AppendLine("(value) });");
                else
                    builder.Append("  return useMutation({ mutationFn: () => services.").Append(functionName).AppendLine("() });");
                builder.AppendLine("}").AppendLine();
            }
        }

        return new GeneratedTypeScriptFile("queries.ts", builder.ToString());
    }

    private static string ToCamelCase(string value) => string.IsNullOrEmpty(value) ? "request" : char.ToLowerInvariant(value[0]) + value[1..];
    private static bool UsesBody(string method) => method.ToUpperInvariant() is "POST" or "PUT" or "PATCH";
    private static string NormalizeType(string type) => string.IsNullOrWhiteSpace(type) || type == "System.Void" ? "void" : type.Replace("global::", string.Empty, StringComparison.Ordinal);
}
