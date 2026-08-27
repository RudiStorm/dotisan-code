# Authorization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close out the v0.2.0 authentication release and add a focused v0.3 authorization foundation using standard ASP.NET Core policies, Identity role claims, and explicit generated CRUD permissions.

**Architecture:** v0.2.0 changes only release metadata and documentation. v0.3 keeps authentication opt-in, adds editable generated permission constants and explicit policy registration to authenticated templates, and makes generated resource endpoints call `RequireAuthorization` with code-defined permission names. Identity remains the persistence boundary for users, roles, and role claims; no custom DI, ORM, configuration replacement, or runtime reflection discovery is introduced.

**Tech Stack:** .NET 8, ASP.NET Core Minimal APIs, ASP.NET Core Identity, EF Core SQLite, ASP.NET Core authorization policies, xUnit, `WebApplicationFactory`, in-memory SQLite, Vue/Vite, existing Dotisan generator and CLI projects.

**Spec:** `docs/superpowers/specs/2026-08-27-authorization-design.md`

## Global Constraints

- Preserve existing user changes and keep the current opt-in authentication boundary.
- Generated source must remain ordinary, inspectable C#; no runtime endpoint or permission discovery.
- Use standard ASP.NET Core Identity, authorization policies, EF Core, configuration, and dependency injection.
- Use the repository’s existing naming and test conventions.
- Keep plain templates unchanged by authorization-specific generation.
- Run focused tests after each implementation batch and record exact verification results.

## Task 1: Close out the v0.2.0 release metadata

**Files:**

- Modify `src/Dotisan.Cli/DotisanApplication.cs`.
- Modify `src/Dotisan.Cli/Dotisan.Cli.csproj`.
- Modify `src/Dotisan.Generators/TemplateFiles.cs`.
- Modify `README.md` and `docs/quickstart.md`.
- Extend the relevant CLI and generator tests.

- [ ] Add failing assertions for the CLI version, package version, and generated frontend package version being `0.2.0`.
- [ ] Update those version values and align release examples in the documentation.
- [ ] Run focused CLI/generator tests and `dotnet build Dotisan.sln --configuration Release --no-restore`.
- [ ] Commit as `release: close out v0.2.0`.

## Task 2: Add the generated permission contract

**Files:**

- Modify `src/Dotisan.Core/EndpointContract.cs` and its tests.
- Modify `src/Dotisan.Generators/TemplateFiles.cs`.
- Extend `tests/Dotisan.Generators.Tests/TemplateGenerationTests.cs` or the closest existing generator test.

- [ ] Add failing contract tests proving permission is rejected without authorization and accepted with authorization.
- [ ] Enforce those invariants in `EndpointOptions` without changing existing endpoint metadata behavior.
- [ ] Generate `Authorization/Permissions.cs` for authenticated projects with `ClaimType`, `ProfileView`, and an `All` collection.
- [ ] Verify plain projects do not receive authorization source.
- [ ] Run focused Core and generator tests.
- [ ] Commit as `feat: add generated permission contract`.

The generated contract must be:

```csharp
public static class Permissions
{
    public const string ClaimType = "permission";
    public const string ProfileView = "profile.view";
    public static IReadOnlyList<string> All { get; } = [ProfileView];
}
```

## Task 3: Register standard authorization policies

**Files:**

- Modify the authenticated `Program.cs` template in `src/Dotisan.Generators/TemplateFiles.cs`.
- Extend generated-project assertions in `tests/Dotisan.Generators.Tests`.

- [ ] Add failing generation assertions for `AddRoles<IdentityRole>()` and explicit policy registration.
- [ ] Register Identity roles and one policy per `Permissions.All` entry using `RequireClaim(Permissions.ClaimType, permission)`.
- [ ] Preserve the existing cookie authentication and Identity setup.
- [ ] Generate an authenticated project and verify it restores/builds.
- [ ] Commit as `feat: register generated authorization policies`.

## Task 4: Add the authenticated authorization probe endpoint

**Files:**

- Modify the authenticated template in `src/Dotisan.Generators/TemplateFiles.cs`.
- Extend generated authentication integration tests.

- [ ] Add failing assertions for `Features/Authorization/AuthorizationEndpoints.cs`.
- [ ] Generate `GET /api/authorization/profile`, protected by `RequireAuthorization(Permissions.ProfileView)`.
- [ ] Register it only in authenticated projects; plain projects must not expose it.
- [ ] Verify the endpoint returns a stable small payload for an authorized caller.
- [ ] Commit as `feat: add generated authorization probe`.

## Task 5: Make authenticated resource scaffolding permission-aware

**Files:**

- Modify `src/Dotisan.Generators/GoldenTemplateGenerator.cs`.
- Extend or add `tests/Dotisan.Generators.Tests/ScaffoldingTests.cs`.
- Modify generated template files in `src/Dotisan.Generators/TemplateFiles.cs` only where required.

- [ ] Add failing scaffolding tests for authenticated CRUD permission constants and endpoint requirements.
- [ ] Read `authentication: enabled` from `dotisan.config` through the existing configuration path.
- [ ] For authenticated projects, generate explicit `customers.view`, `customers.create`, `customers.update`, and `customers.delete` constants and full GET/POST/PUT/DELETE resource endpoints with explicit `RequireAuthorization` calls.
- [ ] Keep the current plain-project list/create behavior and source boundary intact.
- [ ] Verify a scaffolded authenticated Customer project builds and its generated endpoint manifest remains inspectable.
- [ ] Commit as `feat: protect generated resource endpoints with permissions`.

## Task 6: Cover role-claim authorization end to end

**Files:**

- Modify generated authentication integration tests in `src/Dotisan.Generators/TemplateFiles.cs`.
- Extend generator smoke/integration tests as needed.

- [ ] Add failing generated tests for unauthenticated `401`, authenticated-without-permission `403`, and a role-claim caller receiving `200`.
- [ ] Seed a standard `IdentityRole` with `RoleManager<IdentityRole>` and attach the permission using `AddClaimAsync`.
- [ ] Sign in a test user through the existing Identity test host and verify policy evaluation through HTTP.
- [ ] Run generated authenticated restore, build, and test commands.
- [ ] Commit as `test: cover generated authorization flows`.

## Task 7: Document v0.3 authorization behavior

**Files:**

- Modify `README.md`.
- Modify `docs/quickstart.md`.
- Modify the generated README template in `src/Dotisan.Generators/TemplateFiles.cs`.

- [ ] Document the authentication opt-in, generated permission constants, role claims, `401`/`403` behavior, and the explicit source files users edit.
- [ ] State that admin UI/API, tenancy, audit, jobs, telemetry, external providers, and runtime reflection discovery remain out of scope.
- [ ] Add the v0.2.0 release note and v0.3 authorization preview note without claiming unfinished commands exist.
- [ ] Run documentation terminology checks for `RequireAuthorization`, `permission`, `RoleManager`, and `403`.
- [ ] Commit as `docs: document v0.3 authorization foundation`.

## Task 8: Final verification and release handoff

- [ ] Read `superpowers:verification-before-completion` and run fresh verification before claiming completion.
- [ ] Run `dotnet restore Dotisan.sln`, `dotnet build Dotisan.sln --configuration Release`, and `dotnet test Dotisan.sln --configuration Release --verbosity minimal`.
- [ ] Generate plain and authenticated projects with `dotisan new`, including npm installation, then run generated restore/build/test and `npm run build` where dependencies permit.
- [ ] Scaffold an authenticated Customer resource and rebuild/test the generated project.
- [ ] Verify `dotisan --version` reports `0.2.0` and the v0.3 authorization source boundary is correct.
- [ ] Run `git diff --check`, inspect `git status --short --branch`, and record exact blockers such as npm audit findings or unavailable `dotnet ef` tooling.
- [ ] Commit any final verification-only documentation changes and report all commits, files, commands, results, and limitations.

## Plan Self-Review

- Coverage: tasks cover the v0.2 release closeout, permission contract, policy registration, probe endpoint, resource CRUD enforcement, integration tests, documentation, and verification from the approved design.
- Consistency: generated names and APIs are consistent across tasks: `Permissions.ClaimType`, `Permissions.ProfileView`, `Permissions.All`, `IdentityRole`, `RoleManager<IdentityRole>`, `RequireAuthorization`, and `EndpointOptions.Permission`.
- Scope: no admin UI/API, tenancy, audit, jobs, OpenTelemetry, external providers, custom persistence, or runtime reflection discovery is introduced.
- Testability: each behavior begins with a focused failing test and ends with a focused or generated-project verification command.
- Placeholder scan: the plan contains no unresolved TBD/TODO or vague “handle edge cases” steps.
