# v0.4 Audit Foundation Design

## Status

Approved design for the next roadmap slice after the v0.3 authorization foundation.

The current repository has completed the v0.3 authorization implementation but still reports version `0.2.0`. The implementation begins by closing out the v0.3.0 release metadata, then adds the v0.4 audit foundation described here.

## Goals

- Align CLI, package, generated frontend, and documentation versions to `0.3.0`.
- Add a standard, inspectable audit persistence model to every generated project.
- Capture actor, tenant placeholder, timestamp, resource, resource ID, operation, changed fields, trace ID, and correlation ID.
- Capture generated resource operations and generated authentication security operations through explicit source calls.
- Enable auditing by default while allowing standard ASP.NET Core configuration to disable writes.
- Keep the generated implementation compile-time-oriented, testable, and editable by the application team.

## Non-goals

This slice does not add:

- an audit query API, audit admin UI, or permission-gated audit browser;
- event sourcing or a domain event bus;
- tenancy behavior or tenant resolution;
- background jobs, scheduling, OpenTelemetry setup, or external observability services;
- custom dependency injection, ORM, configuration, or migration abstractions;
- runtime reflection or assembly scanning;
- automatic auditing of arbitrary user-written endpoints that do not explicitly opt in;
- generated EF Core migrations.

## Constitutional constraints

- Generated applications remain ordinary ASP.NET Core applications using standard dependency injection, configuration, Minimal APIs, EF Core, and Identity.
- Generated audit source is written into the application and remains directly editable.
- Endpoint and audit behavior is explicit in generated C#; there is no hidden runtime discovery.
- EF Core remains the persistence boundary and `dotnet ef` remains the migration-authoring tool.
- Audit records are not silently discarded when enabled and persistence fails.
- Existing plain/authenticated source boundaries remain intact except for the shared audit infrastructure.

## Versioning

The first implementation task changes these existing values from `0.2.0` to `0.3.0`:

- `DotisanApplication.Version`;
- the CLI package version;
- the generated Vue package version;
- release examples and generated-project documentation.

The audit implementation itself is documented as the v0.4 foundation. Its implementation must not claim that later roadmap slices are complete.

## Generated audit model

Each generated project contains:

```text
src/<Name>.Api/Auditing/AuditEntry.cs
src/<Name>.Api/Auditing/IAuditWriter.cs
src/<Name>.Api/Auditing/AuditWriter.cs
```

`AuditEntry` has the following application-owned EF Core properties:

```text
Id             Guid
ActorId        string?
TenantId       string?
EntityType     string
EntityId       string?
Action         string
Changes        string
TraceId        string?
CorrelationId  string?
CreatedAt      DateTimeOffset
```

The generated `AppDbContext` exposes `DbSet<AuditEntry> AuditEntries`. The authenticated Identity context remains a partial class so resource and audit extensions can be added as separate inspectable files.

`Changes` is JSON serialized with `System.Text.Json`. The generated writer accepts an explicit read-only field map. Read operations use an empty map; create, update, and delete operations include the fields known by the generated endpoint. The generated Customer example records the `Name` field and update old/new values.

## Audit writer contract

The generated application defines a small local contract rather than adding a framework-level abstraction to Dotisan.Core:

```csharp
public interface IAuditWriter
{
    Task RecordAsync(
        HttpContext httpContext,
        string entityType,
        string? entityId,
        string action,
        IReadOnlyDictionary<string, object?> changes,
        CancellationToken cancellationToken);
}
```

`AuditWriter` uses the injected generated `AppDbContext` and `IConfiguration`.

- If `Audit:Enabled` is false, `RecordAsync` returns without writing.
- Otherwise it creates an `AuditEntry`, adds it to `AuditEntries`, and saves it through EF Core.
- `ActorId` is read from `ClaimTypes.NameIdentifier`.
- `TenantId` is null until the tenancy slice supplies a standard tenant context.
- `TraceId` is read from `Activity.Current?.TraceId`.
- `CorrelationId` is read from the first `X-Correlation-ID` request header, falling back to `HttpContext.TraceIdentifier`.
- `CreatedAt` is `DateTimeOffset.UtcNow`.
- Persistence exceptions are allowed to propagate so enabled audit failures remain visible.

The generated app registers `IAuditWriter` as a scoped service and adds `Audit:Enabled: true` to the standard generated `appsettings.json`. Users can set `Audit__Enabled=false` or an equivalent standard configuration value to disable writes.

## Capture points and data flow

Generated endpoints receive `IAuditWriter` and `HttpContext` as ordinary Minimal API parameters. Each endpoint performs its normal operation, then records an explicit audit action.

Resource actions use the resource CLR name as `EntityType` and the entity ID when available:

```text
customers.list    Customers  null  read
customers.read    Customer   id    read
customers.create  Customer   id    create
customers.update  Customer   id    update
customers.delete  Customer   id    delete
```

The exact action strings are generated source constants or stable literals, documented in tests, and not inferred from route names at runtime.

Generated authentication endpoints record security operations using `EntityType = "Security"`, a nullable entity ID, and stable actions for registration, login success, login failure, registration denial, and logout. Failed login records do not include passwords or raw credential input.

The writer is called explicitly after the business operation has produced its known result. User-written endpoints are not automatically audited; they can inject `IAuditWriter` and call the same contract when desired.

## Configuration and migrations

Auditing is generated and enabled by default for both plain and authenticated projects. The runtime setting belongs in standard ASP.NET Core configuration, not in a replacement configuration system.

The generator does not create or apply migrations. Generated-project documentation instructs users to author and review an initial audit migration with normal tooling, for example:

```powershell
dotnet ef migrations add InitialAudit --project src\MyApp.Api
dotnet ef database update --project src\MyApp.Api
```

`dotisan migrate` continues to apply existing migrations only.

## Testing strategy

- Core or generator tests assert the generated audit model, writer contract, default configuration, service registration, and plain/authenticated source boundaries.
- Generated authenticated integration tests use the existing in-memory SQLite test host and inspect `AuditEntries` through a test-only factory helper.
- Security tests verify actor ID, action, trace ID, correlation header fallback, and that failed login entries do not contain credentials.
- Authenticated scaffolding tests verify explicit audit calls for CRUD operations and changed-field data for update operations.
- A configuration test verifies `Audit:Enabled=false` suppresses writes.
- Full repository build/test, generated API tests, npm installation, and Vue/Vite builds are rerun.

## Documentation

Repository and generated-project documentation will describe:

- the v0.3.0 release alignment;
- the default-enabled `Audit:Enabled` setting and standard override;
- the `AuditEntry` fields and explicit capture points;
- the required standard EF Core migration workflow;
- the fact that audit is not event sourcing and has no admin/query UI yet;
- the deferred tenancy, jobs, scheduling, observability, and external integration slices.

## Acceptance criteria

- `dotisan --version` reports `0.3.0` after the release closeout.
- Plain and authenticated generated applications contain buildable audit source and default-enabled audit configuration.
- Authenticated generated integration tests prove security audit records and actor/trace/correlation fields.
- Authenticated resource scaffolding emits explicit audit calls for all generated CRUD operations and preserves prior resource permissions when multiple resources are scaffolded.
- Disabling `Audit:Enabled` prevents audit writes without replacing ASP.NET Core configuration.
- Repository and generated-project verification commands pass, with npm audit findings and unavailable local tools recorded exactly.
