export type ApiFieldErrors = Record<string, string[]>;
export type ProblemDetails = { type?: string; title?: string; status?: number; detail?: string; instance?: string; code?: string; correlationId?: string; errors?: ApiFieldErrors; };

function normalizeFieldErrors(errors: ApiFieldErrors | undefined): ApiFieldErrors {
  return Object.entries(errors ?? {}).reduce<ApiFieldErrors>((result, [key, messages]) => { result[key.charAt(0).toLowerCase() + key.slice(1)] = messages; return result; }, {});
}
function getErrorMessage(status: number, problem: ProblemDetails, fieldErrors: ApiFieldErrors): string {
  if (status === 401) return "Invalid email or password.";
  if (Object.keys(fieldErrors).length > 0) return "Please fix the highlighted fields.";
  return problem.detail ?? (problem.title && problem.title !== "One or more validation errors occurred." ? problem.title : "Request failed.");
}

export class ApiError extends Error {
  public readonly fieldErrors: ApiFieldErrors;
  public readonly correlationId?: string;
  constructor(public readonly status: number, public readonly problem: ProblemDetails) {
    const fieldErrors = normalizeFieldErrors(problem.errors);
    super(getErrorMessage(status, problem, fieldErrors));
    this.correlationId = problem.correlationId;
    this.fieldErrors = fieldErrors;
  }
}

async function request<T>(url: string, init: RequestInit = {}): Promise<T> {
  const response = await fetch(url, { ...init, credentials: "include", signal: init.signal, headers: { Accept: "application/json", "X-Correlation-ID": crypto.randomUUID(), ...(init.body ? { "Content-Type": "application/json" } : {}), ...init.headers } });
  if (!response.ok) {
    const problem = await response.json().catch(() => ({ title: response.statusText }));
    throw new ApiError(response.status, problem);
  }
  return response.status === 204 ? undefined as T : await response.json() as T;
}

