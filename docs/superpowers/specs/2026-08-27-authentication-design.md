# Dotisan v0.2 Authentication Design

## Goal

Add an opt-in, browser-oriented authentication foundation to generated Dotisan applications using standard ASP.NET Core Identity, EF Core, cookie authentication, and explicit Minimal API endpoints.

## Scope

This slice implements the first authentication increment for `dotisan new --auth yes`:

- `ApplicationUser` based on `IdentityUser`.
- Identity persistence in the existing `AppDbContext`.
- Standard Identity password and lockout configuration.
- Cookie authentication and authorization middleware in the normal ASP.NET Core pipeline.
- Explicit account endpoints:
  - `POST /api/account/register`
  - `POST /api/account/login`
  - `POST /api/account/logout`
  - `GET /api/account/me`
- Registration policy from the existing project options:
  - public registration permits registration;
  - invite-only and disabled registration reject registration with a documented problem response.
- Authentication integration tests covering registration policy, duplicate email, login, logout, current-user state, and anonymous access.
- Generated-project documentation and configuration explaining cookie authentication and migration requirements.

The default `dotisan new` path remains unauthenticated and unchanged in behavior.

## Explicitly out of scope

Password reset, email delivery and verification, TOTP MFA, recovery codes, session management UI, admin security UI, roles, permissions, tenancy, external providers, and frontend account pages are separate v0.2 slices. The endpoint and service boundaries must leave room for those features without pretending they are implemented.

## Architecture

The generated API remains a recognizable ASP.NET Core application:

```text
Program.cs
  ├─ AddDbContext<AppDbContext>()
  ├─ AddIdentityCore<ApplicationUser>() + AddEntityFrameworkStores<AppDbContext>()
  ├─ AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
  ├─ AddAuthorization()
  ├─ UseAuthentication()
  ├─ UseAuthorization()
  └─ MapAccountEndpoints()
```

`AppDbContext` changes from `DbContext` to `IdentityDbContext<ApplicationUser>`, using the normal Microsoft Identity EF Core model. No custom user store, DI container, ORM, configuration system, or token format is introduced.

Account endpoints use `UserManager<ApplicationUser>` and `SignInManager<ApplicationUser>` through normal constructor/parameter injection. Password validation, lockout, cookie issuance, and sign-out remain owned by ASP.NET Core Identity.

## Cookie and browser security boundary

The login cookie is HTTP-only and uses the standard application cookie scheme. The generated API enables HTTPS redirection and configures the cookie to be `HttpOnly`, `SameSite=Lax`, and `SecurePolicy=SameAsRequest` for local HTTP development while preserving secure behavior under HTTPS.

State-changing account endpoints validate antiforgery tokens through the standard `IAntiforgery` service. A small `GET /api/account/antiforgery` endpoint issues the normal antiforgery cookie and returns the request token for the Vue client; the client sends that token in the configured header. This keeps the browser flow explicit and avoids inventing a separate CSRF mechanism.

`/api/account/register`, `/api/account/login`, and `/api/account/antiforgery` allow anonymous access. `/api/account/logout` and `/api/account/me` require authentication, with `/me` returning `401 Unauthorized` for anonymous callers.

## Request and response contracts

The generated account endpoint file is vertical and inspectable. Its public contracts are:

```text
RegisterRequest(string Email, string Password)
LoginRequest(string Email, string Password, bool RememberMe)
CurrentUserResponse(string Id, string Email)
```

Success responses use ordinary HTTP semantics:

- register: `200 OK` with the current user after sign-in;
- login: `200 OK` with the current user;
- logout: `204 No Content`;
- current user: `200 OK`.

Failures use ProblemDetails:

- invalid input: `400 Bad Request` with field-level errors;
- invalid credentials: `401 Unauthorized` without revealing whether an email exists;
- disabled or invite-only registration: `403 Forbidden` with a stable error code;
- duplicate or Identity validation failure: `400 Bad Request` with safe, field-level errors.

Identity error descriptions must not expose sensitive account state beyond the documented contract.

## Configuration

`dotisan.config` remains orchestration-only. The auth selection is recorded as the existing project-generation setting and does not replace ASP.NET configuration.

Generated `appsettings.json` contains only non-secret Identity defaults. Password policy and cookie behavior are configured in explicit generated code. Production secrets and data-protection key persistence remain environment/deployment concerns and are documented, not committed.

The authenticated template adds the EF Core Identity package and a design-time package reference. It does not create a migration. Developers author and review the first migration with normal `dotnet ef migrations add InitialIdentity` and apply it with `dotisan migrate`.

## Testing strategy

- Generator tests verify that `--auth yes` emits Identity files, package references, middleware calls, and no auth files when auth is disabled.
- Core tests verify registration policy and stable account option values.
- API integration tests use `WebApplicationFactory<Program>` with an in-memory SQLite connection and a disposable database, exercising the actual HTTP pipeline, Identity stores, cookies, antiforgery, and ProblemDetails responses.
- Existing unauthenticated generated-project build tests remain green.
- Tests never assert implementation details such as private Identity internals; they assert HTTP behavior and generated source contracts.

## Compatibility and migration

Existing unauthenticated projects are not mutated by this feature. New authenticated projects require an initial Identity migration before the database is used. The CLI does not silently create migrations.

The generated API remains .NET 8/net8.0 because the repository pins that target. Package versions remain centrally managed in the generated project and in the repository where applicable.

## Acceptance criteria

From a clean checkout, the following is true:

```text
dotisan new AuthApp --auth yes --yes
dotnet restore AuthApp/AuthApp.sln
dotnet build AuthApp/AuthApp.sln
dotnet ef migrations add InitialIdentity --project AuthApp/src/AuthApp.Api
dotnet ef database update --project AuthApp/src/AuthApp.Api
dotnet test AuthApp/AuthApp.sln
```

The generated authenticated API builds, its account integration tests pass, anonymous requests are rejected where required, successful login establishes the standard cookie, and `dotisan new AuthApp --auth no` continues to generate the existing unauthenticated shape.
