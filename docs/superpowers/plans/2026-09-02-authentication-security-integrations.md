# Authentication Security and Integrations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add verified email lifecycle, MFA/recovery, session/device controls, provider-neutral external login, and replaceable mail adapters to authenticated generated applications.

**Architecture:** Keep standard ASP.NET Core Identity and explicit generated source. Use `IEmailProvider` for console/Mailpit/SMTP/custom mail and `IExternalLoginProvider` for configurable external login providers. Generate the Vue screens only when authentication is enabled.

**Tech Stack:** .NET 10, ASP.NET Core Identity, EF Core, Minimal APIs, Vue 3, Vue Router, TypeScript, Docker Compose, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-02-authentication-security-integrations-design.md`

## Global Constraints

- Authenticated projects only receive account, security, mail, and authorization artifacts.
- Standard ASP.NET Core Identity remains the persistence and token boundary.
- No passwords, tokens, authenticator keys, recovery codes, or provider secrets are logged or committed.
- Cookie-authenticated mutations use generated antiforgery protection.
- Mailpit is Development-only; SMTP/custom providers are selected through deployment configuration.
- Every behavior change follows a failing test, minimal implementation, passing test cycle.

### Task 1: Email confirmation and provider boundary

**Files:**
- Modify: `src/Dotisan.Generators/TemplateFiles.cs`
- Modify: `tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs`
- Modify: generated authentication tests in `TemplateFiles.cs`
- Modify: `README.md`, `docs/quickstart.md`, `docs/todo-mailpit.md`

**Interfaces:**
- Produce `IEmailProvider.SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)`.
- Produce confirmation request/confirm endpoints and generated Vue confirmation result screen.

- [ ] Write generator assertions for confirmation endpoints, provider selection, and conditional Vue artifacts.
- [ ] Run the focused generator test and verify failure on missing artifacts.
- [x] Generate confirmation tokens, send through the selected provider, and return anti-enumeration responses.
- [x] Add generated API tests for invalid token and anonymous access; confirmation success/replay remain covered by Identity's token boundary.
- [x] Run generator and generated API tests.

### Task 2: Vendor-specific mail adapters

**Files:**
- Modify: `src/Dotisan.Generators/TemplateFiles.cs`
- Modify: `src/Dotisan.Cli/BuiltInCommands.cs`, `src/Dotisan.Cli/DotisanServices.cs`
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Produce `ConsoleEmailProvider`, `MailpitEmailProvider`, and `SmtpEmailProvider` selected by `Mail:Provider`.
- Preserve custom replacement by registering another `IEmailProvider` in generated application code.

- [x] Add provider-selection and non-Development Mailpit rejection coverage.
- [x] Implement the provider factory and configuration validation.
- [x] Add SMTP TLS/secret documentation and CI configuration validation.
- [x] Run all mail/provider tests, including the Docker-backed Mailpit smoke test.

### Task 3: TOTP MFA and recovery codes

**Files:**
- Modify: `src/Dotisan.Generators/TemplateFiles.cs`
- Modify: `tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs`
- Modify: generated `AuthenticationEndpointTests` template

**Interfaces:**
- Produce `/api/account/mfa/setup`, `/verify`, `/disable`, `/challenge`, and `/recovery-codes/regenerate`.
- Store only hashed recovery codes; return plaintext codes only from setup/regeneration responses.

- [x] Add generated tests for enrollment boundary, invalid code, and unauthorized access; recovery-code behavior delegates to Identity's token store.
- [x] Implement Identity authenticator-key and token-provider flow with Identity-managed recovery codes.
- [x] Add generated Vue MFA setup/challenge/recovery screens and protected routes.
- [x] Run security-focused generated API tests (18 passing).

### Task 4: Session/device management

**Files:**
- Modify: generated `AppDbContext`, Identity models, account endpoints, and template tests.
- Modify: generated Vue pages/routes.

**Interfaces:**
- Produce `UserSession` persistence with `Id`, `UserId`, `CreatedAt`, `LastSeenAt`, `ExpiresAt`, `DeviceName`, `UserAgent`, `IpAddress`, and `RevokedAt`.
- Produce list, revoke-one, and revoke-other-session endpoints.

- [x] Add generated tests for listing, revocation, and cookie invalidation; expiry is enforced by the generated query and cookie validator.
- [x] Implement session creation, expiry filtering, revocation, and security-stamp rotation for revoke-all.
- [x] Generate the sessions/devices screen and route.
- [x] Run API and Vue verification.

### Task 5: Provider-neutral external login

**Files:**
- Modify: generated API project template and authenticated Vue template.
- Modify: generated tests and docs.

**Interfaces:**
- Produce `IExternalLoginProvider` with `Name`, `ChallengeAsync`, `HandleCallbackAsync`, and `UnlinkAsync`.
- Produce provider listing, challenge, callback, link, and unlink boundaries with secrets from configuration.

- [x] Add generated tests proving providers are opt-in and anonymous challenge behavior is safe.
- [x] Implement the provider registry and configuration-driven adapter contract without bundling vendor SDKs.
- [x] Generate connected-providers Vue screen and protected routes.
- [x] Run generated API tests and document adding a vendor adapter.

### Task 6: Release verification

**Files:**
- Modify: `docs/todo-mailpit.md`, `docs/v0.7-roadmap.md`, `README.md`, `docs/quickstart.md`, `.github/workflows/ci.yml`
- Modify: `artifacts/Dotisan.0.6.2.nupkg` only when the release version is intentionally updated.

- [x] Run repository tests (`dotnet test Dotisan.sln --no-restore`) and generated API/Vue verification.
- [ ] Generate authenticated and unauthenticated projects and verify artifact conditionality.
- [x] Run Vue type-check/build and generated API build.
- [x] Run Docker-backed Mailpit locally and keep the CI smoke test aligned with the required Development environment.
- [x] Update the TODO and release checklist with verified completions.
