using System.Text;
using Dotisan.Core;

namespace Dotisan.TypeScript;

public static class FetchClientGenerator
{
    public static GeneratedTypeScriptFile Generate(ContractManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var builder = new StringBuilder();
        var models = manifest.Models.Select(model => model.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (models.Length > 0)
            builder.Append("import type { ").Append(string.Join(", ", models)).AppendLine(" } from \"./models\";\n");
        builder.AppendLine("export type ApiFieldErrors = Record<string, string[]>;");
        builder.AppendLine("export type ProblemDetails = { type?: string; title?: string; status?: number; detail?: string; instance?: string; code?: string; correlationId?: string; errors?: ApiFieldErrors; };\n");
        builder.AppendLine("function normalizeFieldErrors(errors: ApiFieldErrors | undefined): ApiFieldErrors {");
        builder.AppendLine("  return Object.entries(errors ?? {}).reduce<ApiFieldErrors>((result, [key, messages]) => { result[key.charAt(0).toLowerCase() + key.slice(1)] = messages; return result; }, {});");
        builder.AppendLine("}");
        builder.AppendLine("function getErrorMessage(status: number, problem: ProblemDetails, fieldErrors: ApiFieldErrors): string {");
        builder.AppendLine("  if (status === 401) return \"Invalid email or password.\";");
        builder.AppendLine("  if (Object.keys(fieldErrors).length > 0) return \"Please fix the highlighted fields.\";");
        builder.AppendLine("  return problem.detail ?? (problem.title && problem.title !== \"One or more validation errors occurred.\" ? problem.title : \"Request failed.\");");
        builder.AppendLine("}\n");
        builder.AppendLine("export class ApiError extends Error {");
        builder.AppendLine("  public readonly fieldErrors: ApiFieldErrors;");
        builder.AppendLine("  public readonly correlationId?: string;");
        builder.AppendLine("  constructor(public readonly status: number, public readonly problem: ProblemDetails) {");
        builder.AppendLine("    const fieldErrors = normalizeFieldErrors(problem.errors);");
        builder.AppendLine("    super(getErrorMessage(status, problem, fieldErrors));");
        builder.AppendLine("    this.correlationId = problem.correlationId;");
        builder.AppendLine("    this.fieldErrors = fieldErrors;");
        builder.AppendLine("  }");
        builder.AppendLine("}").AppendLine();
        builder.AppendLine("async function request<T>(url: string, init: RequestInit = {}): Promise<T> {");
        builder.AppendLine("  const response = await fetch(url, { ...init, credentials: \"include\", signal: init.signal, headers: { Accept: \"application/json\", \"X-Correlation-ID\": crypto.randomUUID(), ...(init.body ? { \"Content-Type\": \"application/json\" } : {}), ...init.headers } });");
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
                builder.AppendLine("    headers: { \"Content-Type\": \"application/json\" },");
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

        if (route.EndsWith('}'))
            builder.Append('"');

        return builder.ToString();
    }

    private static bool UsesBody(string method) => method.ToUpperInvariant() is "POST" or "PUT" or "PATCH";

    private static string NormalizeType(string type) => string.IsNullOrWhiteSpace(type) || type == "System.Void" ? "void" : type.Replace("global::", string.Empty, StringComparison.Ordinal);

    private static string ToCamelCase(string value) => string.IsNullOrEmpty(value) ? "request" : char.ToLowerInvariant(value[0]) + value[1..];
}
