using System.Text;
using Dotisan.Core;

namespace Dotisan.TypeScript;

public static class FetchClientGenerator
{
    public static GeneratedTypeScriptFile Generate(ContractManifest manifest)
        => Generate(manifest, "services.ts", "./models");

    internal static GeneratedTypeScriptFile Generate(ContractManifest manifest, string path, string modelsImport)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var builder = new StringBuilder();
        var models = manifest.Models.Select(model => model.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (models.Length > 0)
            builder.Append("import type { ").Append(string.Join(", ", models)).Append(" } from \"").Append(modelsImport).AppendLine("\";\n");
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
            var pathParameters = metadata?.PathParameters ?? [];
            var queryParameters = metadata?.QueryParameters ?? [];
            var parameters = string.Join(", ", pathParameters.Select(parameter =>
                $"{parameter.Name}: {TypeScriptTypeMapper.Map(parameter.Type, parameter.Nullable, optional: false)}"));
            if (queryParameters.Count > 0)
            {
                var queryType = string.Join("; ", queryParameters.Select(parameter =>
                    $"{parameter.Name}{(parameter.Optional ? "?" : string.Empty)}: {TypeScriptTypeMapper.Map(parameter.Type, parameter.Nullable, optional: false)}"));
                parameters = string.IsNullOrEmpty(parameters)
                    ? $"query: {{ {queryType} }} = {{}}"
                    : parameters + $", query: {{ {queryType} }} = {{}}";
            }
            if (UsesBody(endpoint.Method) && requestType != "void")
                parameters = string.IsNullOrEmpty(parameters) ? $"body: {requestType}" : parameters + $", body: {requestType}";
            parameters = string.IsNullOrEmpty(parameters) ? "options: RequestInit = {}" : parameters + ", options: RequestInit = {}";
            builder.Append("export async function ").Append(functionName).Append('(').Append(parameters).Append("): Promise<").Append(responseType).AppendLine(">");
            builder.AppendLine("{");
            if (queryParameters.Count > 0)
            {
                builder.AppendLine("  const search = new URLSearchParams();");
                foreach (var parameter in queryParameters)
                {
                    builder.Append("  if (query.").Append(parameter.Name).Append(" !== undefined) search.set(\"")
                        .Append(parameter.Name).Append("\", String(query.").Append(parameter.Name).AppendLine("));");
                }
            }
            builder.Append("  return request<").Append(responseType).Append(">(").Append(BuildUrl(endpoint.Route, pathParameters, queryParameters)).AppendLine(", {");
            builder.Append("    method: \"").Append(endpoint.Method.ToUpperInvariant()).AppendLine("\",");
            if (UsesBody(endpoint.Method) && requestType != "void")
                builder.AppendLine("    headers: { \"Content-Type\": \"application/json\" },");
            if (UsesBody(endpoint.Method) && requestType != "void")
                builder.AppendLine("    body: JSON.stringify(body),");
            builder.AppendLine("    ...options");
            builder.AppendLine("  });");
            builder.AppendLine("}").AppendLine();
        }

        return new GeneratedTypeScriptFile(path, builder.ToString());
    }

    private static string BuildUrl(string route, IReadOnlyList<EndpointParameterMetadata> pathParameters, IReadOnlyList<EndpointParameterMetadata> queryParameters)
    {
        var builder = new StringBuilder("`");
        var index = 0;
        while (index < route.Length)
        {
            var start = route.IndexOf('{', index);
            if (start < 0)
            {
                builder.Append(route[index..].Replace("`", "\\`", StringComparison.Ordinal));
                break;
            }

            var end = route.IndexOf('}', start);
            var token = route[(start + 1)..end].Trim().TrimStart('*').TrimEnd('?');
            var separator = token.IndexOfAny([':', '=']);
            var name = separator >= 0 ? token[..separator] : token;
            if (!pathParameters.Any(parameter => parameter.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Route parameter '{name}' is not described by contract metadata.");
            builder.Append(route[index..start]).Append("${encodeURIComponent(String(").Append(name).Append("))}");
            index = end + 1;
        }

        builder.Append('`');
        if (queryParameters.Count > 0)
            builder.Append(" + (search.size ? `?${search}` : \"\")");

        return builder.ToString();
    }

    private static bool UsesBody(string method) => method.ToUpperInvariant() is "POST" or "PUT" or "PATCH";

    private static string NormalizeType(string type) => string.IsNullOrWhiteSpace(type) || type == "System.Void" ? "void" : type.Replace("global::", string.Empty, StringComparison.Ordinal);

    private static string ToCamelCase(string value) => string.IsNullOrEmpty(value) ? "request" : char.ToLowerInvariant(value[0]) + value[1..];
}
