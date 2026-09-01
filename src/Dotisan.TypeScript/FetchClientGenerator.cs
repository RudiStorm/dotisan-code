using System.Text;
using Dotisan.Core;

namespace Dotisan.TypeScript;

public static class FetchClientGenerator
{
    public static GeneratedTypeScriptFile Generate(ContractManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var builder = new StringBuilder("export type ProblemDetails = { title?: string; detail?: string; status?: number; errors?: Record<string, string[]>; };\n\n");
        builder.AppendLine("export class ApiError extends Error {");
        builder.AppendLine("  constructor(public readonly status: number, public readonly problem: ProblemDetails) {");
        builder.AppendLine("    super(problem.detail ?? problem.title ?? \"Request failed\");");
        builder.AppendLine("  }");
        builder.AppendLine("}").AppendLine();
        builder.AppendLine("async function request<T>(url: string, init: RequestInit = {}): Promise<T> {");
        builder.AppendLine("  const response = await fetch(url, { ...init, credentials: \"include\", signal: init.signal, headers: { Accept: \"application/json\", \"X-Correlation-ID\": crypto.randomUUID(), ...init.headers } });");
        builder.AppendLine("  if (!response.ok) {");
        builder.AppendLine("    const problem = await response.json().catch(() => ({ title: response.statusText }));");
        builder.AppendLine("    throw new ApiError(response.status, problem);");
        builder.AppendLine("  }");
        builder.AppendLine("  return response.status === 204 ? undefined as T : await response.json() as T;");
        builder.AppendLine("}").AppendLine();

        for (var index = 0; index < manifest.Endpoints.Count; index++)
        {
            var endpoint = manifest.Endpoints[index];
            var metadata = manifest.EndpointMetadata?[index];
            var functionName = ToCamelCase(endpoint.Name);
            var responseType = NormalizeType(endpoint.Response);
            var requestType = NormalizeType(endpoint.Request);
            var parameters = metadata is null
                ? string.Empty
                : string.Join(", ", metadata.PathParameters.Concat(metadata.QueryParameters)
                    .Select(parameter => $"{parameter.Name}: {TypeScriptTypeMapper.Map(parameter.Type, parameter.Nullable, parameter.Optional)}"));
            if (UsesBody(endpoint.Method) && requestType != "void")
                parameters = string.IsNullOrEmpty(parameters) ? $"body: {requestType}" : parameters + $", body: {requestType}";
            parameters = string.IsNullOrEmpty(parameters) ? "options: RequestInit = {}" : parameters + ", options: RequestInit = {}";
            builder.Append("export async function ").Append(functionName).Append('(').Append(parameters).Append("): Promise<").Append(responseType).AppendLine(">");
            builder.AppendLine("{");
            builder.Append("  return request<").Append(responseType).Append(">(").Append(BuildUrl(endpoint.Route)).AppendLine(", {");
            builder.Append("    method: \"").Append(endpoint.Method.ToUpperInvariant()).AppendLine("\",");
            if (UsesBody(endpoint.Method) && requestType != "void")
                builder.AppendLine("    body: JSON.stringify(body),");
            builder.AppendLine("    ...options");
            builder.AppendLine("  });");
            builder.AppendLine("}").AppendLine();
        }

        return new GeneratedTypeScriptFile("services.ts", builder.ToString());
    }

    private static string BuildUrl(string route)
    {
        var builder = new StringBuilder("\"");
        var index = 0;
        while (index < route.Length)
        {
            var start = route.IndexOf('{', index);
            if (start < 0)
            {
                builder.Append(route[index..].Replace("\"", "\\\"", StringComparison.Ordinal)).Append('"');
                break;
            }

            builder.Append(route[index..start]).Append("\" + ").Append(route[(start + 1)..route.IndexOf('}', start)]).Append(" + \"");
            index = route.IndexOf('}', start) + 1;
        }

        return builder.ToString();
    }

    private static bool UsesBody(string method) => method.ToUpperInvariant() is "POST" or "PUT" or "PATCH";

    private static string NormalizeType(string type) => string.IsNullOrWhiteSpace(type) || type == "System.Void" ? "void" : type.Replace("global::", string.Empty, StringComparison.Ordinal);

    private static string ToCamelCase(string value) => string.IsNullOrEmpty(value) ? "request" : char.ToLowerInvariant(value[0]) + value[1..];
}
