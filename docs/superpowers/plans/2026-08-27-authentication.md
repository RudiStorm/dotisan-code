# Authentication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an opt-in ASP.NET Core Identity and cookie-authentication foundation to generated Dotisan applications.

**Architecture:** Authenticated templates will use the existing EF Core `AppDbContext` as an `IdentityDbContext<ApplicationUser>`, normal ASP.NET Core Identity stores, the application cookie scheme, and explicit vertical Minimal API account endpoints. The unauthenticated template remains unchanged; the generator conditionally emits the Identity package, model, endpoint, configuration, middleware, and integration-test support only when `ProjectOptions.AuthenticationEnabled` is true.

**Tech Stack:** .NET 8 / ASP.NET Core Minimal APIs, ASP.NET Core Identity, EF Core SQLite, `WebApplicationFactory`, in-memory SQLite, xUnit, existing Dotisan generator and CLI projects.

**Spec:** `docs/superpowers/specs/2026-08-27-authentication-design.md`

## Global Constraints

- Use standard ASP.NET Core Identity, EF Core, dependency injection, configuration, authentication, authorization, antiforgery, and ProblemDetails; do not create replacement abstractions.
- Authentication is opt-in; `dotisan new` without `--auth yes` must retain the current unauthenticated generated shape.
- Generated code must remain ordinary, editable ASP.NET Core source and must not create migrations automatically.
- Registration policy is controlled by `ProjectOptions.Registration`; public permits registration, invite-only and disabled reject it with a safe `403` ProblemDetails response.
- Cookie mutations require the standard ASP.NET Core antiforgery service and request-token header flow.
- Use the repository’s `net10.0` target and centrally managed package versions.
- Every implementation task follows RED → GREEN → REFACTOR and ends with a focused test run.

---

### Task 1: Add authentication package/version contracts

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/Dotisan.Generators/TemplateFiles.cs`
- Test: `tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs`

**Interfaces:**
- Consumes: `ProjectOptions.AuthenticationEnabled` and existing `TemplateFiles.Create`.
- Produces: authenticated generated projects containing `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.Mvc.Testing`, and `Microsoft.Data.Sqlite` test package references with centrally managed versions.

- [ ] **Step 1: Write the failing generator test**

Add a test that generates `AuthApp` with `ProjectOptions.Quick("AuthApp", output) with { AuthenticationEnabled = true }` and asserts the API project contains:

```csharp
Assert.Contains("Microsoft.AspNetCore.Identity.EntityFrameworkCore", apiProject);
Assert.Contains("Microsoft.AspNetCore.Mvc.Testing", testProject);
Assert.Contains("Microsoft.Data.Sqlite", testProject);
```

Also add an assertion that an unauthenticated `PlainApp` API project does not contain `Microsoft.AspNetCore.Identity.EntityFrameworkCore`.

- [ ] **Step 2: Run the focused test to verify RED**

Run:

```powershell
dotnet test tests\Dotisan.Generators.Tests\Dotisan.Generators.Tests.csproj --no-restore --configuration Release --filter FullyQualifiedName~Authentication
```

Expected: FAIL because the template has no conditional Identity package or auth test package.

- [ ] **Step 3: Implement the minimal package emission**

Add centrally managed versions for the Identity EF package, `Microsoft.AspNetCore.Mvc.Testing`, and `Microsoft.Data.Sqlite`. Make `ApiProject(name, authenticationEnabled)` add:

```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />
```

when auth is enabled. Make `ApiTestsProject(name, authenticationEnabled)` add the MVC testing and SQLite packages only when auth is enabled.

- [ ] **Step 4: Run the focused test to verify GREEN**

Run the same filtered test. Expected: PASS with the existing generator test also passing.

- [ ] **Step 5: Commit**

```powershell
git add Directory.Packages.props src/Dotisan.Generators/TemplateFiles.cs tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs
git commit -m "feat: add authentication template dependencies"
```

### Task 2: Add the Identity user and EF Core context shape

**Files:**
- Modify: `src/Dotisan.Generators/TemplateFiles.cs`
- Test: `tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs`

**Interfaces:**
- Consumes: conditional authenticated template from Task 1.
- Produces: `Identity/ApplicationUser.cs` and an authenticated `AppDbContext : IdentityDbContext<ApplicationUser>`.

- [ ] **Step 1: Write the failing generator test**

Extend the authenticated generator test with:

```csharp
Assert.Contains("Identity/ApplicationUser.cs", paths);
Assert.Contains("IdentityDbContext<ApplicationUser>", dbContext);
Assert.Contains("public sealed class ApplicationUser : IdentityUser", user);
Assert.DoesNotContain("Identity/ApplicationUser.cs", unauthenticatedPaths);
```

- [ ] **Step 2: Run the focused test to verify RED**

Run the generator authentication filter. Expected: FAIL because the generated context is currently a plain `DbContext` and no user file exists.

- [ ] **Step 3: Implement the conditional Identity model/context**

Change the template factory to emit `Data/AppDbContext.cs` from an auth-aware helper. For authenticated projects, generate:

```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AppName.Api.Identity;

namespace AppName.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options);
```

Emit `Identity/ApplicationUser.cs`:

```csharp
using Microsoft.AspNetCore.Identity;

namespace AppName.Api.Identity;

public sealed class ApplicationUser : IdentityUser
{
}
```

Keep the existing partial plain context for unauthenticated projects so existing resource scaffolding continues to compile.

- [ ] **Step 4: Run the focused test to verify GREEN**

Run the generator authentication filter and the existing generator tests. Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/Dotisan.Generators/TemplateFiles.cs tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs
git commit -m "feat: add generated Identity user context"
```

### Task 3: Add explicit account endpoint source

**Files:**
- Modify: `src/Dotisan.Generators/TemplateFiles.cs`
- Test: `tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs`

**Interfaces:**
- Consumes: `ApplicationUser`, `ProjectOptions.Registration`, and normal `UserManager<ApplicationUser>` / `SignInManager<ApplicationUser>` injection.
- Produces: `Features/Account/AccountEndpoints.cs` with `MapAccountEndpoints`, `RegisterRequest`, `LoginRequest`, and `CurrentUserResponse`.

- [ ] **Step 1: Write the failing generator test**

Assert the authenticated template contains:

```csharp
Assert.Contains("Features/Account/AccountEndpoints.cs", paths);
Assert.Contains("MapAccountEndpoints", accountEndpoints);
Assert.Contains("RegisterRequest", accountEndpoints);
Assert.Contains("LoginRequest", accountEndpoints);
Assert.Contains("CurrentUserResponse", accountEndpoints);
Assert.Contains("PasswordSignInAsync", accountEndpoints);
Assert.Contains("UserManager<ApplicationUser>", accountEndpoints);
Assert.DoesNotContain("AccountEndpoints.cs", unauthenticatedPaths);
```

- [ ] **Step 2: Run the focused test to verify RED**

Run the generator authentication filter. Expected: FAIL because account source is absent.

- [ ] **Step 3: Implement the vertical account endpoint**

Generate a single account file with these routes:

```csharp
public static void MapAccountEndpoints(this IEndpointRouteBuilder endpoints, RegistrationPolicy registrationPolicy)
{
    var group = endpoints.MapGroup("/api/account");
    group.MapGet("/antiforgery", IssueAntiforgery).AllowAnonymous();
    group.MapPost("/register", Register).AllowAnonymous().RequireAntiforgery();
    group.MapPost("/login", Login).AllowAnonymous().RequireAntiforgery();
    group.MapPost("/logout", Logout).RequireAuthorization().RequireAntiforgery();
    group.MapGet("/me", Me).RequireAuthorization();
}
```

Use `UserManager` and `SignInManager` directly. Public registration creates and signs in the user. Invite-only and disabled registration return `Results.Problem` with status `403` and extension `code` set to `registration_unavailable`. Invalid credentials return `Results.Unauthorized()` without revealing whether the account exists. Identity creation errors return `Results.ValidationProblem` with field errors. `Me` reads `ClaimTypes.NameIdentifier` and `ClaimTypes.Email` from the authenticated principal and returns `CurrentUserResponse`.

Use `IAntiforgery.GetAndStoreTokens(HttpContext)` for the token endpoint and configure the request-token header as `X-XSRF-TOKEN` in the host wiring task.

- [ ] **Step 4: Run the focused test to verify GREEN**

Run the generator authentication filter. Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/Dotisan.Generators/TemplateFiles.cs tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs
git commit -m "feat: generate account authentication endpoints"
```

### Task 4: Wire Identity, cookies, antiforgery, and account mapping

**Files:**
- Modify: `src/Dotisan.Generators/TemplateFiles.cs`
- Test: `tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs`

**Interfaces:**
- Consumes: authenticated context and account endpoint from Tasks 2–3.
- Produces: standard service registration and pipeline wiring in authenticated generated `Program.cs`.

- [ ] **Step 1: Write the failing generator test**

Assert authenticated `Program.cs` contains the following ordered concepts:

```csharp
Assert.Contains("AddIdentityCore<ApplicationUser>", program);
Assert.Contains("AddEntityFrameworkStores<AppDbContext>", program);
Assert.Contains("AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)", program);
Assert.Contains("AddAuthorization", program);
Assert.Contains("UseAuthentication", program);
Assert.Contains("UseAuthorization", program);
Assert.Contains("UseAntiforgery", program);
Assert.Contains("MapAccountEndpoints", program);
```

Assert unauthenticated `Program.cs` does not contain `AddIdentityCore<ApplicationUser>` or `MapAccountEndpoints`.

- [ ] **Step 2: Run the focused test to verify RED**

Run the generator authentication filter. Expected: FAIL because the current program has only EF Core and the health endpoint.

- [ ] **Step 3: Implement authenticated host wiring**

Generate authenticated `Program.cs` with standard registrations:

```csharp
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 8;
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddSignInManager()
.AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");
```

Place `app.UseAntiforgery()` after `UseAuthentication`/`UseAuthorization` and before endpoint mapping, then call `app.MapAccountEndpoints(registrationPolicy)` before the health/fallback routes. Keep the unauthenticated program exactly on its current standard path.

- [ ] **Step 4: Run the focused test to verify GREEN**

Run the generator authentication filter and the full generator test project. Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/Dotisan.Generators/TemplateFiles.cs tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs
git commit -m "feat: wire generated cookie authentication"
```

### Task 5: Add generated authentication integration tests

**Files:**
- Modify: `src/Dotisan.Generators/TemplateFiles.cs`
- Test: `tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs`

**Interfaces:**
- Consumes: generated authenticated API from Tasks 1–4.
- Produces: `AuthenticationEndpointTests.cs` and an authenticated test factory using `WebApplicationFactory<Program>` and an open in-memory SQLite connection.

- [ ] **Step 1: Write the failing generated-project test assertion**

Add generator assertions that auth-enabled output contains `AuthenticationEndpointTests.cs` and `WebApplicationFactory<Program>`, while auth-disabled output does not contain that file.

The generated test content must include these real HTTP tests:

```csharp
[Fact]
public async Task Me_requires_authentication()
{
    using var client = factory.CreateClient();
    var response = await client.GetAsync("/api/account/me");
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}

[Fact]
public async Task Public_registration_logs_the_user_in_and_me_returns_the_account()
{
    using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
    var antiforgery = await GetAntiforgeryToken(client);
    using var register = new HttpRequestMessage(HttpMethod.Post, "/api/account/register");
    register.Headers.Add("X-XSRF-TOKEN", antiforgery);
    register.Content = JsonContent.Create(new { email = "person@example.com", password = "Password1!" });

    var registration = await client.SendAsync(register);
    Assert.Equal(HttpStatusCode.OK, registration.StatusCode);

    var me = await client.GetAsync("/api/account/me");
    Assert.Equal(HttpStatusCode.OK, me.StatusCode);
}

[Fact]
public async Task Invalid_login_does_not_reveal_account_existence()
{
    using var client = factory.CreateClient();
    var antiforgery = await GetAntiforgeryToken(client);
    using var login = new HttpRequestMessage(HttpMethod.Post, "/api/account/login");
    login.Headers.Add("X-XSRF-TOKEN", antiforgery);
    login.Content = JsonContent.Create(new { email = "missing@example.com", password = "wrong" });

    var response = await client.SendAsync(login);
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}
```

Add tests for duplicate email (`400`), logout (`204` followed by `401` on `/me`), and disabled/invite-only registration (`403` with `registration_unavailable`).

- [ ] **Step 2: Run the focused generated-template test to verify RED**

Run the generator authentication filter. Expected: FAIL because no generated auth test files exist.

- [ ] **Step 3: Implement the factory and tests in the template**

Generate a test factory that opens one `SqliteConnection("DataSource=:memory:")`, keeps it open for the factory lifetime, removes the application `DbContextOptions<AppDbContext>` registration, and re-adds `AddDbContext<AppDbContext>(options => options.UseSqlite(connection))`. Call `Database.EnsureCreated()` in the factory before tests run. The factory must set `ASPNETCORE_ENVIRONMENT=Testing` and create a fresh `HttpClient` per test.

Generate `AuthenticationEndpointTests.cs` with the tests above plus duplicate, logout, and registration-policy cases. Keep all generated test code editable and free of reflection or custom test framework abstractions.

- [ ] **Step 4: Run a generated authenticated project test**

Generate an auth-enabled project, restore it, build it, and run:

```powershell
dotnet test AuthApp\AuthApp.sln --configuration Release
```

Expected: the generated authentication integration tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/Dotisan.Generators/TemplateFiles.cs tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs
git commit -m "test: add generated authentication integration coverage"
```

### Task 6: Update documentation and authenticated migration guidance

**Files:**
- Modify: `README.md`
- Modify: `docs/quickstart.md`
- Modify: `src/Dotisan.Generators/TemplateFiles.cs`
- Test: `tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs`

**Interfaces:**
- Consumes: authenticated generator behavior from Tasks 1–5.
- Produces: documented `dotisan new --auth yes` workflow and explicit initial Identity migration instructions.

- [ ] **Step 1: Write the failing documentation/template assertion**

Add generator assertions that the authenticated README contains:

```csharp
Assert.Contains("--auth yes", readme);
Assert.Contains("InitialIdentity", readme);
Assert.Contains("dotisan migrate", readme);
Assert.Contains("does not create migrations", readme);
```

- [ ] **Step 2: Run the focused test to verify RED**

Run the generator authentication filter. Expected: FAIL because the current README is auth-agnostic.

- [ ] **Step 3: Implement the documentation updates**

Document:

```powershell
dotisan new AuthApp --auth yes --yes
dotnet ef migrations add InitialIdentity --project src\AuthApp.Api
dotisan migrate
dotisan dev
```

State clearly that cookies require HTTPS in production, secrets and data-protection keys are deployment concerns, and the CLI never creates migrations silently. Update the repository README and quickstart to identify authentication as the first v0.2 slice while leaving password reset, MFA, authorization, tenancy, and external providers deferred.

- [ ] **Step 4: Run the focused test to verify GREEN**

Run the generator authentication filter and the full generator test project. Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add README.md docs/quickstart.md src/Dotisan.Generators/TemplateFiles.cs tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs
git commit -m "docs: add v0.2 authentication quickstart"
```

### Task 7: Full verification and release checkpoint

**Files:**
- Verify: all solution projects and generated authenticated/unauthenticated projects.

- [ ] **Step 1: Restore and build the repository**

```powershell
dotnet restore Dotisan.sln
dotnet build Dotisan.sln --no-restore --configuration Release
```

Expected: exit code 0, no warnings, no errors.

- [ ] **Step 2: Run the complete repository test suite**

```powershell
dotnet test Dotisan.sln --no-build --configuration Release --verbosity minimal
```

Expected: all existing and new tests pass.

- [ ] **Step 3: Run authenticated and unauthenticated generated-project smoke tests**

For both configurations, generate with `--no-restore`, restore the generated solution, build, and run tests. Confirm that authenticated output includes account behavior and unauthenticated output builds without Identity packages/files.

- [ ] **Step 4: Verify source and repository hygiene**

```powershell
git diff --check
git status --short --branch
```

Expected: no whitespace errors and a clean worktree after the final commit.

- [ ] **Step 5: Commit the final verified checkpoint**

```powershell
git log --oneline -6
```

Record the commit hash, verification commands/results, any NuGet/npm/tooling blockers, and the exact v0.2 work remaining.

## Plan self-review

- **Spec coverage:** Identity model/context, cookie pipeline, explicit account routes, registration policy, antiforgery, ProblemDetails semantics, integration tests, opt-in generation, migration boundary, and documentation are covered by Tasks 1–6.
- **Out-of-scope protection:** password recovery, email verification, MFA, roles, permissions, tenancy, external providers, and frontend account pages are explicitly deferred in the design and not included in this plan.
- **Placeholder scan:** every task has concrete files, signatures, commands, and expected results; no incomplete task instructions remain.
- **Type consistency:** all tasks use `ApplicationUser`, `AppDbContext`, `RegistrationPolicy`, `MapAccountEndpoints`, `WebApplicationFactory<Program>`, and the package names consistently.
- **Repository compatibility:** unauthenticated template behavior and existing resource scaffolding are explicitly tested throughout.
