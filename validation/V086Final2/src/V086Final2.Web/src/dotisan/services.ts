import type { AntiforgeryResponse, CurrentUserResponse, EmailConfirmationRequest, EmailConfirmationResendRequest, ExternalLoginProviderDescriptor, ExternalProvidersResponse, LoginRequest, LoginResponse, MfaCodeRequest, MfaSetupResponse, PasswordResetConfirmRequest, PasswordResetRequest, RecoveryCodesResponse, RegisterRequest, SessionInfo, SessionsResponse } from "./models";

export type ApiFieldErrors = Record<string, string[]>;
export type ProblemDetails = { title?: string; detail?: string; status?: number; errors?: ApiFieldErrors; };

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
  constructor(public readonly status: number, public readonly problem: ProblemDetails) {
    const fieldErrors = normalizeFieldErrors(problem.errors);
    super(getErrorMessage(status, problem, fieldErrors));
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

export async function issueAntiforgery(options: RequestInit = {}): Promise<AntiforgeryResponse>
{
  return request<AntiforgeryResponse>("/api/account/antiforgery", {
    method: "GET",
    ...options
  });
}

export async function confirmEmail(body: EmailConfirmationRequest, options: RequestInit = {}): Promise<void>
{
  return request<void>("/api/account/email-confirmation/confirm", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
    ...options
  });
}

export async function resendConfirmation(body: EmailConfirmationResendRequest, options: RequestInit = {}): Promise<void>
{
  return request<void>("/api/account/email-confirmation/resend", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
    ...options
  });
}

export async function externalLoginCallback(provider: string, options: RequestInit = {}): Promise<void>
{
  return request<void>("/api/account/external/" + provider + "/callback", {
    method: "GET",
    ...options
  });
}

export async function externalLoginChallenge(provider: string, options: RequestInit = {}): Promise<void>
{
  return request<void>("/api/account/external/" + provider + "/challenge", {
    method: "GET",
    ...options
  });
}

export async function linkExternalProvider(provider: string, options: RequestInit = {}): Promise<void>
{
  return request<void>("/api/account/external/" + provider + "/link", {
    method: "POST",
    ...options
  });
}

export async function listExternalProviders(options: RequestInit = {}): Promise<ExternalProvidersResponse>
{
  return request<ExternalProvidersResponse>("/api/account/external/providers", {
    method: "GET",
    ...options
  });
}

export async function unlinkExternalProvider(provider: string, options: RequestInit = {}): Promise<void>
{
  return request<void>("/api/account/external/" + provider + "/link", {
    method: "DELETE",
    ...options
  });
}

export async function login(body: LoginRequest, options: RequestInit = {}): Promise<LoginResponse>
{
  return request<LoginResponse>("/api/account/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
    ...options
  });
}

export async function logout(options: RequestInit = {}): Promise<void>
{
  return request<void>("/api/account/logout", {
    method: "POST",
    ...options
  });
}

export async function me(options: RequestInit = {}): Promise<CurrentUserResponse>
{
  return request<CurrentUserResponse>("/api/account/me", {
    method: "GET",
    ...options
  });
}

export async function challengeMfa(body: MfaCodeRequest, options: RequestInit = {}): Promise<void>
{
  return request<void>("/api/account/mfa/challenge", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
    ...options
  });
}

export async function disableMfa(options: RequestInit = {}): Promise<void>
{
  return request<void>("/api/account/mfa/disable", {
    method: "POST",
    ...options
  });
}

export async function regenerateRecoveryCodes(options: RequestInit = {}): Promise<RecoveryCodesResponse>
{
  return request<RecoveryCodesResponse>("/api/account/mfa/recovery-codes/regenerate", {
    method: "POST",
    ...options
  });
}

export async function getMfaSetup(options: RequestInit = {}): Promise<MfaSetupResponse>
{
  return request<MfaSetupResponse>("/api/account/mfa/setup", {
    method: "GET",
    ...options
  });
}

export async function verifyMfa(body: MfaCodeRequest, options: RequestInit = {}): Promise<void>
{
  return request<void>("/api/account/mfa/verify", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
    ...options
  });
}

export async function confirmPasswordReset(body: PasswordResetConfirmRequest, options: RequestInit = {}): Promise<void>
{
  return request<void>("/api/account/password-reset/confirm", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
    ...options
  });
}

export async function requestPasswordReset(body: PasswordResetRequest, options: RequestInit = {}): Promise<void>
{
  return request<void>("/api/account/password-reset/request", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
    ...options
  });
}

export async function register(body: RegisterRequest, options: RequestInit = {}): Promise<void>
{
  return request<void>("/api/account/register", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
    ...options
  });
}

export async function revokeAllSessions(options: RequestInit = {}): Promise<void>
{
  return request<void>("/api/account/sessions/revoke-all", {
    method: "POST",
    ...options
  });
}

export async function revokeSession(id: string, options: RequestInit = {}): Promise<void>
{
  return request<void>("/api/account/sessions/" + id + "", {
    method: "DELETE",
    ...options
  });
}

export async function listSessions(options: RequestInit = {}): Promise<SessionsResponse>
{
  return request<SessionsResponse>("/api/account/sessions", {
    method: "GET",
    ...options
  });
}

