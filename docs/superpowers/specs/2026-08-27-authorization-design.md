# Dotisan v0.3 Authorization Design

## Status

This design follows the completed v0.2 authentication slice. Before v0.3 implementation begins, v0.2 receives a release closeout: version metadata moves from `0.1.0` to `0.2.0`, package and documentation examples are aligned, and the existing repository/generated-project verification suite is rerun.

The v0.3 feature is the first authorization slice from `003-authorization` in the Spec Kit. It is intentionally separate from audit logging, tenancy, jobs, OpenTelemetry, and external identity providers.

## Goals

- Add standard ASP.NET Core authorization to authenticated generated projects.
- Represent code-defined permissions as named policies.
- Use ASP.NET Core Identity roles and role claims for persisted permission assignments.
- Protect generated resource CRUD endpoints with explicit permission policies.
- Keep authorization metadata visible in source and in the existing endpoint manifest.
- Return the standard `401 Unauthorized` and `403 Forbidden` semantics.
- Preserve the unauthenticated template and existing native ASP.NET Core boundaries.

## Non-goals

This slice does not add:

- an authorization replacement framework or custom DI/container abstraction;
- an admin UI or role/permission management API;
- tenancy-aware permissions;
- audit logging;
- external providers, MFA, password recovery, or email verification;
- frontend authorization pages;
- runtime assembly scanning or reflection-based endpoint discovery.

Developers can seed or manage role assignments with the standard `RoleManager<IdentityRole>`, `UserManager<ApplicationUser>`, and EF Core APIs until a later administration slice exists.

## Chosen architecture

### Generated authorization model

When `ProjectOptions.AuthenticationEnabled` is true, the template emits an editable `Authorization/Permissions.cs` file containing code-defined permission names. The initial names are stable constants for generated resource conventions, for example:

```csharp
public static class Permissions
{
    public const string CustomersView = "customers.view";
    public const string CustomersCreate = "customers.create";
    public const string CustomersUpdate = "customers.update";
    public const string CustomersDelete = "customers.delete";
}
```

The exact resource-specific constants are added by `make:resource` when a resource is scaffolded. Permission names are source-controlled contracts; they are not discovered from database rows.

The authenticated host uses the standard Identity role services with `IdentityRole` and role claims. A role claim with type `permission` and a code-defined permission value is the persisted assignment. On sign-in, the standard Identity claims principal includes role claims, and named ASP.NET Core policies require the corresponding permission claim.

The generated host registers policies explicitly with `AddAuthorization`. No custom policy provider is introduced. A small generated helper may centralize policy registration and claim type constants, but it remains ordinary application source code.

### Endpoint protection

The existing endpoint contract already exposes `Authorization` and `Permission` metadata, and the manifest already serializes both fields. v0.3 uses those fields consistently:

- generated resource endpoints explicitly call `RequireAuthorization` with their permission policy;
- generated account endpoints retain their current explicit authorization behavior;
- endpoint metadata and manifest entries identify whether authorization is required and which permission is expected;
- the plain template has no Identity, role, permission, or authorization dependency beyond its existing unchanged host shape.

The endpoint mapper remains explicit. No runtime code searches assemblies or infers routes from permission names.

### Registration and user experience

`dotisan new` does not gain a second authorization prompt. Authentication remains the opt-in boundary: authenticated projects receive the authorization foundation; unauthenticated projects remain plain. The generated README documents how to seed a role claim with standard Identity APIs and explains that no administration UI is generated yet.

## Request flow

```text
Generated endpoint
  -> RequireAuthorization(permission policy)
  -> ASP.NET Core authentication resolves cookie principal
  -> ASP.NET Core authorization checks permission claim
  -> 200/2xx when allowed
  -> 401 when anonymous
  -> 403 when authenticated without the permission
```

The existing cookie and antiforgery behavior remains unchanged. Authorization executes through the normal ASP.NET Core middleware and endpoint metadata pipeline.

## Failure semantics

- Anonymous access to a protected endpoint returns `401 Unauthorized`.
- An authenticated user without the required permission returns `403 Forbidden`.
- A permission referenced by generated source must be a code-defined constant; the generator/scaffolder returns a clear generation error for invalid or missing permission identifiers.
- Duplicate permission constants or duplicate policy registrations are rejected during generation or compilation rather than silently merged.
- No permission assignment is implicitly granted to a newly registered user.

## Testing strategy

### Repository tests

- Core tests validate permission naming and endpoint metadata invariants.
- ASP.NET Core tests validate explicit authorization convention behavior.
- Generator tests verify authorization files and policy registration are emitted only for authenticated projects.
- Scaffolder tests verify resource GET/POST endpoints receive the expected permission policies and the plain project remains unchanged.
- CLI tests verify v0.2 version closeout and unchanged command behavior.

### Generated integration tests

The authenticated generated test project will use the existing `WebApplicationFactory<Program>` and in-memory SQLite setup. It will cover:

- anonymous access to a protected resource returns 401;
- an authenticated user without the permission receives 403;
- a seeded role claim with the required permission receives 200/2xx;
- account endpoints continue to register, log in, read `/me`, and log out;
- the unauthenticated generated project still builds and passes its health test.

The generated test host continues to use ephemeral data protection keys so tests do not depend on machine-wide key-ring permissions.

## v0.2 release closeout acceptance

Before starting v0.3 implementation:

1. `DotisanApplication.Version`, CLI package metadata, and generated template package metadata report `0.2.0`.
2. README and quickstart commands use the released version.
3. `dotnet restore Dotisan.sln` succeeds.
4. `dotnet build Dotisan.sln --configuration Release` succeeds with zero warnings and errors.
5. The full repository test suite passes.
6. Authenticated and unauthenticated generated projects restore, build, and test successfully.
7. A generated npm project installs dependencies during `dotisan new` and its Vue/Vite build succeeds when registry access is available.

## v0.3 authorization acceptance

The v0.3 slice is complete when:

1. An authenticated generated project contains editable permission constants and explicit ASP.NET Core policy registration.
2. Generated resource endpoints enforce view/create/update/delete permission policies.
3. Standard Identity role claims can grant a generated permission.
4. Anonymous and unauthorized requests produce the expected 401/403 responses.
5. The endpoint manifest preserves authorization and permission metadata.
6. The plain template remains free of authentication/authorization-specific files and dependencies.
7. Repository and generated integration tests pass, with no custom DI, ORM, configuration replacement, or runtime reflection discovery introduced.
