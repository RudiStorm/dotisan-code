# Dotisan Spec Kit

## Suggested Structure

```text
.specify/
└── memory/
    └── constitution.md

specs/
└── 001-dotisan-framework/
    ├── spec.md
    ├── plan.md
    ├── tasks.md
    ├── quickstart.md
    ├── data-model.md
    ├── endpoint-manifest.md
    ├── integration-model.md
    └── contracts/
        ├── endpoint-contract.md
        ├── generated-client-contract.md
        └── dotisan-config.md
```

---

# `.specify/memory/constitution.md`

# Dotisan Constitution

## I. Standard .NET First

Dotisan MUST remain a recognizable ASP.NET Core application.

Dotisan MUST NOT replace standard .NET functionality when the standard platform already provides a suitable implementation.

Examples include:

- ASP.NET Core dependency injection
- ASP.NET Core middleware
- endpoint filters
- EF Core
- `ILogger<T>`
- `IOptions<T>`
- ASP.NET configuration
- ASP.NET Identity
- OpenTelemetry
- `HttpClientFactory`

A developer MUST be able to use normal .NET documentation to configure or extend these technologies inside a Dotisan application.

---

## II. Cohesion Over Reinvention

Dotisan exists to make established technologies feel like parts of one cohesive framework.

Dotisan SHOULD:

- configure good defaults
- establish conventions
- generate repetitive code
- wire components together
- provide consistent CLI workflows

Dotisan SHOULD NOT:

- unnecessarily wrap mature libraries
- hide standard configuration
- introduce proprietary versions of existing .NET concepts
- require developers to understand Dotisan internals for ordinary application maintenance

---

## III. Batteries Included

A generated Dotisan application SHOULD include sensible solutions for the common requirements of production applications.

The developer SHOULD NOT need to spend the first days of a project assembling infrastructure before business development can begin.

The framework SHOULD progressively provide:

- persistence
- validation
- frontend integration
- authentication
- authorization
- auditing
- background work
- scheduling
- observability
- notifications
- mail
- storage
- caching
- imports
- exports
- webhooks
- testing
- production diagnostics

---

## IV. Convention First, Override When Necessary

The common case MUST require minimal configuration.

Dotisan SHOULD infer obvious information from naming and structure.

Examples:

```text
CreateCustomer
→ POST /api/customers

GetCustomer
→ GET /api/customers/{id}

UpdateCustomer
→ PUT /api/customers/{id}
```

Every important convention MUST have an explicit override when an application requires different behavior.

---

## V. One Endpoint, One File

The default backend application structure MUST use single-file vertical endpoints.

An endpoint MAY contain:

- Request
- Response
- Validator
- Handler
- endpoint metadata

Example structure:

```text
Features/
└── Customers/
    ├── CreateCustomer.cs
    ├── GetCustomer.cs
    ├── GetCustomers.cs
    ├── UpdateCustomer.cs
    └── DeleteCustomer.cs
```

Shared abstractions SHOULD only be extracted when genuine reuse or complexity requires them.

---

## VI. Build-Time Automation Over Runtime Magic

Where practical, Dotisan MUST prefer compile-time/source-generated discovery over runtime reflection.

Examples:

- endpoint registration
- handler registration
- validator registration
- CLI command discovery
- endpoint manifest generation

Generated code MUST be inspectable.

---

## VII. Endpoint Manifest Is the Contract

The Dotisan endpoint manifest is the canonical compile-time representation of the API.

It MUST drive:

- endpoint registration
- OpenAPI generation
- TypeScript generation
- validation metadata
- permissions
- API compatibility analysis
- observability metadata
- documentation

OpenAPI is an output of this contract, not the source of Dotisan application metadata.

---

## VIII. Backend Is the Source of Truth

C# request and response contracts are authoritative.

Dotisan MUST generate frontend contracts from backend metadata.

Generated frontend artifacts SHOULD include:

- TypeScript types
- numeric enums
- Zod schemas
- typed API services
- TanStack Query composables

The build MUST detect stale generated contracts.

---

## IX. Generated vs Scaffolded Code

Dotisan MUST distinguish between regeneratable code and developer-owned scaffolded code.

### Regeneratable

```text
src/generated/
```

Examples:

- TypeScript models
- schemas
- API services
- query composables

These files MUST NOT be manually modified.

### Scaffolded

Examples:

- Vue forms
- CRUD pages
- navigation entries
- application-specific endpoints

These are generated once and then become normal application source.

---

## X. Production Safety Without Development Friction

Development SHOULD optimize for speed.

Production SHOULD optimize for predictability and safety.

Examples:

Development:

```text
dotisan migrate
→ generate migration
→ apply migration
```

Production:

```text
dotisan migrate
→ only apply committed migrations
```

Dotisan MUST NOT silently create production schema changes.

---

## XI. Security Is a Default

Production-safe defaults SHOULD be established automatically where practical.

Examples:

- secure cookies
- antiforgery
- restrictive CORS
- HSTS
- CSP
- authentication lockout
- audit logs
- redaction
- rate limiting on sensitive endpoints

Security mechanisms MUST remain standard ASP.NET Core mechanisms underneath.

---

## XII. Observability From Day One

All Dotisan applications MUST be OpenTelemetry-ready.

OpenTelemetry is the standard observability contract.

The development default SHOULD use Aspire Dashboard.

Production destinations MAY include:

- SigNoz
- Azure Monitor
- generic OTLP providers
- other OpenTelemetry-compatible systems

---

## XIII. Local Development Must Be Fast

`dotisan dev` MUST optimize the development loop.

Application processes SHOULD run natively:

- ASP.NET Core
- Vite

Supporting infrastructure MAY run in containers:

- database
- Mailpit
- observability
- optional integrations

---

## XIV. One Golden Template

Dotisan MUST maintain one primary application architecture.

Optional features MUST alter capabilities, not create fundamentally different project structures.

API-only mode MAY omit the Vue frontend while retaining the same backend architecture.

---

## XV. No Premature Frameworks Inside the Framework

Dotisan MUST avoid introducing unnecessary:

- plugin systems
- module systems
- generic billing abstractions
- generic repository layers
- service layers
- custom configuration frameworks

New abstractions MUST solve demonstrated application friction.

---

## XVI. Testable by Default

Framework functionality MUST be test-driven.

Generated applications SHOULD include:

- xUnit
- integration testing
- Testcontainers
- Vitest
- Vue Test Utils
- Playwright

Generated functionality SHOULD include sensible test scaffolding.

---

## XVII. Production Readiness Is Measurable

Dotisan MUST eventually provide:

```text
dotisan doctor --production
```

Production readiness MUST be evaluated through explicit checks rather than vague claims.

---

# `specs/001-dotisan-framework/spec.md`

# Dotisan Framework Specification

## 1. Purpose

Dotisan is a batteries-included application framework for building production-capable applications using C# / ASP.NET Core and Vue.

The framework exists because creating a new ASP.NET Core API and Vue frontend currently requires significant setup before productive feature work can begin.

This setup frequently includes:

- project creation
- EF Core
- validation
- API conventions
- frontend models
- API clients
- frontend query/state handling
- authentication
- observability
- testing
- development orchestration
- deployment preparation

This friction encourages developers to prototype in frameworks such as Laravel and later rewrite applications in .NET.

Dotisan aims to remove that rewrite.

---

# 2. Primary Goal

A developer SHOULD be able to create a new application and begin business development within minutes.

The core workflow is:

```text
dotisan new MyApp
cd MyApp

dotisan make:resource Customer

# modify Customer model

dotisan migrate
dotisan dev
```

The resulting application MUST remain capable of evolving directly into production.

---

# 3. Target Outcomes

For a simple application:

> Nothing → production-capable application within approximately one working day.

For a small/medium application:

> Nothing → production-capable application within approximately one to two weeks.

These are product goals rather than guaranteed development durations.

---

# 4. Non-Goals

Dotisan is NOT intended to:

- replace ASP.NET Core
- replace EF Core
- replace FluentValidation
- replace Vue
- replace OpenTelemetry
- introduce a custom dependency injection container
- introduce a custom ORM
- introduce a custom configuration system
- introduce a proprietary module system
- require microservices
- require Kubernetes
- require Redis
- require distributed infrastructure for small applications

---

# 5. Primary Application Architecture

Default production architecture:

```text
ASP.NET Core
├── API
├── authentication
├── jobs
├── scheduler
├── static Vue frontend
└── health endpoints
```

The Vue production build is copied into ASP.NET `wwwroot`.

The default production result is one deployable application artifact/container.

Development uses separate processes:

```text
dotisan dev
├── dotnet watch
├── Vite
└── supporting infrastructure
```

---

# 6. Default Project Structure

```text
MyApp/
├── src/
│   ├── MyApp.Api/
│   │   ├── Features/
│   │   ├── Data/
│   │   ├── Auth/
│   │   ├── Infrastructure/
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── appsettings.Development.json
│   │
│   └── MyApp.Web/
│       ├── src/
│       ├── package.json
│       └── vite.config.ts
│
├── tests/
│   ├── MyApp.Api.Tests/
│   └── MyApp.Web.Tests/
│
├── dotisan.config
├── global.json
├── .node-version
├── Dockerfile
└── MyApp.sln
```

---

# 7. Project Creation

Command:

```text
dotisan new MyApp
```

First question:

```text
Setup mode:

> Quick
  Advanced
```

## Quick Setup

Must ask:

### Database

```text
> SQLite
  SQL Server
  PostgreSQL
  MySQL
```

SQLite is default.

### Authentication

```text
> Yes
  No
```

If enabled:

```text
Registration:

> Public
  Invite only
  Disabled
```

### Multi-tenancy

```text
> No
  Yes
```

### Package manager

```text
> pnpm
  npm
```

pnpm is recommended.

---

# 8. Backend Endpoint Model

Dotisan uses vertical single-file Minimal API endpoints.

Conceptual example:

```csharp
public static class CreateCustomer : IDotisanEndpoint
{
    public sealed record Request(
        string Name,
        string Email);

    public sealed record Response(
        Guid Id,
        string Name,
        string Email);

    public sealed class Validator : AbstractValidator<Request>
    {
    }

    public sealed class Handler
    {
    }

    public static EndpointOptions Configure()
    {
        return new();
    }
}
```

The final exact API MAY evolve during implementation, but MUST preserve:

- endpoint locality
- nested Request
- nested Response
- nested Validator
- nested Handler
- optional endpoint-local metadata
- DI-resolved handlers

---

# 9. Endpoint Discovery

Endpoints MUST be discovered at compile time.

Dotisan MUST generate endpoint registration.

Application startup SHOULD contain a small explicit call such as:

```text
app.MapDotisanEndpoints();
```

Runtime reflection SHOULD NOT be required for normal endpoint discovery.

---

# 10. Endpoint Manifest

Every build creates a deterministic endpoint manifest.

Each endpoint includes at minimum:

```text
id
name
feature
method
route
request
response
validation
authorization
permission
version
tags
deprecated
```

This manifest is Dotisan's canonical API description.

---

# 11. EF Core

EF Core is the official Dotisan persistence layer.

Dotisan SHOULD provide:

- initial configuration
- provider setup
- conventions
- scaffolding
- migrations

Dotisan MUST NOT hide normal EF Core functionality.

Developers MAY directly use:

```text
DbContext
DbSet
LINQ
EF configuration
dotnet ef
```

---

# 12. FluentValidation

FluentValidation is the standard Dotisan request validation library.

Nested endpoint validators MUST be automatically discovered and executed.

Validation failures MUST become standard `ProblemDetails`.

---

# 13. API Errors

Dotisan uses normal HTTP status semantics.

Examples:

```text
200 OK
201 Created
204 No Content

400 Bad Request
401 Unauthorized
403 Forbidden
404 Not Found
409 Conflict
422 Unprocessable Entity where appropriate
500 Internal Server Error
```

Errors MUST use ASP.NET `ProblemDetails`.

Validation failures MUST expose field-level errors.

Correlation IDs MUST eventually be included.

---

# 14. Database Migrations

`make:resource` MUST NOT create migrations.

Development workflow:

```text
dotisan migrate
```

MAY:

- detect EF model changes
- create migration
- apply migration

Production:

```text
dotisan migrate
```

MUST only apply existing committed migrations.

---

# 15. TypeScript Generation

The endpoint manifest drives TypeScript generation.

Generated files MUST be grouped by feature.

Example:

```text
src/generated/
└── customers/
    ├── models.ts
    ├── schemas.ts
    ├── service.ts
    └── queries.ts
```

---

# 16. Type Mapping

Dotisan MUST correctly support:

- strings
- booleans
- integers
- decimals
- GUIDs
- arrays
- dictionaries where supported
- nested objects
- nullable properties
- optional properties
- numeric enums
- UTC timestamps
- `DateOnly`
- `TimeOnly`

Nullable and optional MUST remain distinct.

---

# 17. Enum Convention

Numeric enums are default.

C#:

```text
Pending = 0
Approved = 1
Rejected = 2
```

TypeScript:

```text
Pending = 0
Approved = 1
Rejected = 2
```

String enums MAY be explicitly enabled later.

---

# 18. Frontend Validation

Portable FluentValidation rules SHOULD generate Zod schemas.

Zod schemas are consumed by VeeValidate.

Backend validation remains authoritative.

---

# 19. Generated API Client

Generated services use standard `fetch`.

A small shared helper handles:

- JSON
- cookies
- errors
- ProblemDetails
- cancellation
- correlation IDs
- antiforgery when auth is enabled

---

# 20. TanStack Query

Generated API clients SHOULD provide TanStack Query composables.

Example:

```text
useCustomers()
useCustomer(id)
useCreateCustomer()
useUpdateCustomer()
useDeleteCustomer()
```

Pinia is reserved for application/client state.

---

# 21. Vue Foundation

The default Vue stack includes:

- Vue 3
- TypeScript
- Vite
- Vue Router
- TanStack Query
- Pinia
- Tailwind
- shadcn-vue
- VeeValidate
- Zod
- Sonner
- Vitest
- Vue Test Utils
- Playwright

---

# 22. UI Scaffolding

Dotisan SHOULD scaffold editable UI code.

Example:

```text
dotisan make:crud Customer
```

May generate:

- list
- table
- search
- pagination
- sorting
- create
- edit
- detail
- delete
- loading state
- empty state
- error state
- routes
- breadcrumbs
- navigation

Scaffolded UI becomes developer-owned.

---

# 23. Development Supervisor

`dotisan dev` MUST orchestrate development.

Expected responsibilities:

```text
API
Vue
Aspire
Mailpit
Database infrastructure
Workers
```

CLI overrides MUST override `dotisan.config`.

Examples:

```text
dotisan dev --lean
dotisan dev --lean --observability
dotisan dev --environment UAT
```

---

# 24. Dotisan Configuration Boundary

`dotisan.config` is orchestration configuration only.

It MAY describe:

- which development services start
- default profile
- observability enabled
- frontend enabled
- worker enabled

It MUST NOT replace:

- appsettings
- environment variables
- ASP.NET options
- EF configuration
- Vite configuration

---

# 25. Observability

OpenTelemetry MUST be configured from the beginning.

Development default:

```text
Aspire Dashboard
```

Production provider is selected independently.

Dotisan SHOULD support easy provider integrations.

---

# 26. Integrations

Integrations are CLI-assisted installation recipes.

Example:

```text
dotisan add:integration signoz
```

An integration MAY:

- install a package
- add standard configuration
- add environment placeholders
- generate explicit registration code
- modify dev orchestration

It MUST NOT become an opaque plugin runtime.

---

# 27. Authentication

If authentication is selected during project creation, Dotisan provides the complete security foundation.

Initial implementation includes:

- ASP.NET Identity
- cookie auth
- login
- logout
- registration policy
- email verification
- password reset
- lockout
- session management
- TOTP MFA
- recovery codes
- user profile
- admin security UI

---

# 28. Authorization

Dotisan uses:

```text
Roles
+
Permissions
+
Policies
```

CRUD permissions are generated conventionally.

Example:

```text
customers.view
customers.create
customers.update
customers.delete
```

Business permissions are explicit.

Example:

```text
customers.approve-credit
```

Code defines permissions.

The database stores assignments.

---

# 29. Audit

Audit logging is enabled by default.

Advanced setup MAY disable it.

Audit captures:

- actor
- tenant
- timestamp
- resource
- resource ID
- operation
- changed fields
- trace ID
- correlation ID
- security operations
- administrative actions

Audit is NOT event sourcing.

---

# 30. Jobs and Scheduling

Wolverine is the preferred durable queue/job implementation.

Dotisan provides a small scheduler.

The scheduler decides when work runs.

Wolverine handles:

- execution
- queues
- durability
- retries
- failure handling
- concurrency

Durable message publication SHOULD use a transactional outbox.

---

# 31. Multi-Tenancy

If selected during project creation, default tenancy is:

```text
Shared database
Shared schema
TenantId
```

Dotisan MUST provide strong tenant filtering and tests against cross-tenant access.

Database-per-tenant MAY be introduced later.

---

# 32. Mail and Notifications

Mail baseline:

```text
SMTP
```

Development:

```text
Mailpit
```

Notifications initially support:

- email
- in-app

SignalR SHOULD provide real-time in-app notification updates.

---

# 33. Storage and Cache

Storage default:

```text
Local filesystem
```

Production providers are integrations.

Cache default:

```text
IMemoryCache
```

Distributed cache providers are integrations.

---

# 34. Production Container

Every normal application SHOULD include a production-ready Dockerfile.

The final production image SHOULD:

- contain ASP.NET runtime only
- contain built Vue assets
- run as non-root
- include health checks
- contain no secrets
- support graceful shutdown

---

# 35. Testing

Backend:

- xUnit
- integration tests
- Testcontainers

Frontend:

- Vitest
- Vue Test Utils
- Playwright

Generated functionality SHOULD include baseline tests where safe.

---

# 36. Production Doctor

Future command:

```text
dotisan doctor --production
```

Must evaluate actual readiness rather than merely printing recommendations.

Results:

```text
PASS
WARNING
BLOCKING
```

---

# 37. Success Criteria

Dotisan is succeeding when this feels dramatically faster than manual setup:

```text
dotisan new MyApp
dotisan make:resource Customer
dotisan migrate
dotisan dev
```

while the generated project remains understandable as ordinary ASP.NET Core + Vue.

---

# `specs/001-dotisan-framework/plan.md`

# Dotisan Technical Plan

## Architecture

Dotisan consists of several focused packages.

```text
Dotisan.Cli
    ↓
Dotisan.Core
    ↓
Dotisan.AspNetCore
    ↓
Dotisan.Generators
    ↓
Endpoint Manifest
    ├── OpenAPI
    ├── TypeScript
    ├── Zod
    ├── API Services
    └── TanStack Query
```

## Initial Internal Packages

```text
Dotisan.Cli
Dotisan.Core
Dotisan.AspNetCore
Dotisan.Generators
Dotisan.TypeScript
Dotisan.Testing
```

Avoid creating more packages until boundaries become necessary.

---

## Build Sequence

### Stage 1 — Framework Skeleton

Build:

- repository
- packages
- CLI host
- testing infrastructure

No application behavior yet.

---

### Stage 2 — Application Creation

Build:

```text
dotisan new
```

Output:

- ASP.NET Core
- Vue
- SQLite
- Vite
- standard project structure
- Dockerfile
- tests

This creates the golden app template.

---

### Stage 3 — Endpoint Model

Build:

```text
IDotisanEndpoint
```

and the single-file endpoint convention.

---

### Stage 4 — Source Generator

Build compile-time endpoint discovery.

Generate:

- route registration
- handlers
- validators

---

### Stage 5 — Manifest

Generate a deterministic endpoint manifest.

Everything after this stage consumes this model.

---

### Stage 6 — Backend Productivity

Add:

- FluentValidation
- EF Core
- `make:resource`
- `make:endpoint`
- migrations

---

### Stage 7 — API Contract

Add:

- OpenAPI
- branded API explorer

---

### Stage 8 — Frontend Contract Generation

Manifest:

```text
↓
TypeScript
↓
Zod
↓
fetch service
↓
TanStack Query
```

---

### Stage 9 — UI Productivity

Add:

- shadcn-vue foundation
- form scaffolding
- CRUD scaffolding
- routing/navigation scaffolding

---

### Stage 10 — Development Experience

Build:

```text
dotisan dev
```

Add Aspire + Mailpit orchestration.

---

### Stage 11 — Production Foundation

Add:

- auth
- authorization
- audit
- health
- OpenTelemetry
- security baseline
- doctor

---

### Stage 12 — Jobs/Infrastructure

Add:

- Wolverine
- scheduler
- outbox
- notifications
- mail
- SignalR
- storage
- caching

---

### Stage 13 — SaaS

Add:

- multi-tenancy
- feature flags
- quotas
- API keys
- settings
- branding
- entitlements

---

### Stage 14 — Business Batteries

Add:

- imports
- exports
- uploads
- webhooks
- search

---

### Stage 15 — Operations and Compliance

Add:

- retention
- privacy workflows
- encryption
- release safety
- SBOM
- upgrade
- CI/CD
- production diagnostics

---

# `specs/001-dotisan-framework/endpoint-manifest.md`

# Endpoint Manifest Specification

The endpoint manifest is Dotisan's canonical API metadata model.

Each endpoint MUST expose:

```text
id
feature
name
method
route
request
response
version
tags
authorization
permission
validation
transaction
idempotency
rateLimit
deprecated
```

## Example

```json
{
  "id": "customers.create",
  "feature": "Customers",
  "name": "CreateCustomer",
  "method": "POST",
  "route": "/api/customers",
  "request": "CreateCustomer.Request",
  "response": "CreateCustomer.Response",
  "authorization": true,
  "permission": "customers.create",
  "version": null,
  "deprecated": false
}
```

The concrete serialized schema MAY evolve before 1.0.

Manifest changes MUST be versioned.

---

# `specs/001-dotisan-framework/contracts/endpoint-contract.md`

# Endpoint Contract

Every Dotisan endpoint MUST:

- implement the endpoint marker contract
- be statically discoverable
- provide a handler
- use normal DI
- have deterministic metadata

An endpoint MAY define:

```text
Request
Response
Validator
Configure()
```

Request/response contracts are endpoint-local unless deliberately extracted.

---

# `specs/001-dotisan-framework/contracts/generated-client-contract.md`

# Generated TypeScript Client Contract

Generated frontend source MUST:

- be deterministic
- be safe to regenerate
- never require manual editing
- preserve backend nullability
- preserve optional semantics
- use numeric enums
- use ISO date/time transport
- generate Zod where possible
- generate typed fetch services
- generate TanStack Query composables

Generated code MUST be separated from scaffolded UI code.

---

# `specs/001-dotisan-framework/contracts/dotisan-config.md`

# `dotisan.config` Contract

`dotisan.config` controls Dotisan orchestration only.

Example conceptual configuration:

```yaml
dev:
  profile: full

  services:
    api: true
    frontend: true
    database: true
    mail: true
    observability: true
    workers: true
```

CLI overrides have highest precedence:

```text
Dotisan defaults
    ↓
dotisan.config
    ↓
CLI overrides
```

Example:

```text
dotisan dev --lean --observability
```

MUST override the project default only for that invocation.

---

# `specs/001-dotisan-framework/data-model.md`

# Initial Framework Data Concepts

These are framework-level concepts, not mandatory tables in every initial release.

## User

Used when authentication is enabled.

Core properties:

```text
Id
Email
FirstName
LastName
DisplayName
Avatar
Locale
Timezone
Active
```

---

## Role

```text
Id
Name
Description
```

---

## Permission

Available permissions are code-defined.

Assignments are persisted.

---

## Tenant

Only when tenancy is enabled.

```text
Id
Name
Status
CreatedAt
```

---

## AuditEntry

```text
Id
ActorId
TenantId
EntityType
EntityId
Action
Changes
TraceId
CorrelationId
CreatedAt
```

---

## Notification

```text
Id
UserId
TenantId
Type
Payload
ReadAt
CreatedAt
```

---

## JobExecution

```text
Id
JobType
TenantId
Status
Progress
StartedAt
CompletedAt
CorrelationId
```

These concepts SHOULD be implemented incrementally rather than all during the first milestone.

---

# `specs/001-dotisan-framework/quickstart.md`

# Dotisan Development Quickstart

## Install

```text
dotnet tool install --global Dotisan
```

## Create Project

```text
dotisan new TodoApp
```

Choose Quick Setup.

Example:

```text
Database: SQLite
Authentication: No
Multi-tenancy: No
Package manager: pnpm
```

## Create Resource

```text
cd TodoApp

dotisan make:resource TodoItem
```

Edit the generated model.

## Database

```text
dotisan migrate
```

## Run

```text
dotisan dev
```

Expected development services:

```text
API
Vue
Aspire
Mailpit where required
```

## Standard Tooling Still Works

```text
dotnet build
dotnet test
dotnet run
pnpm dev
pnpm build
dotnet ef ...
```

Dotisan is the preferred workflow, not a lock-in mechanism.

---

# `specs/001-dotisan-framework/tasks.md`

# Dotisan Initial Implementation Tasks

## Phase 1 — Repository

- [ ] Create solution.
- [ ] Create CLI project.
- [ ] Create Core project.
- [ ] Create ASP.NET integration project.
- [ ] Create generator project.
- [ ] Create TypeScript generator project.
- [ ] Create testing project.
- [ ] Create tests.
- [ ] Configure central dependencies.
- [ ] Configure analyzers.
- [ ] Configure CI.

---

## Phase 2 — CLI

- [ ] Implement CLI host.
- [ ] Implement command registration.
- [ ] Implement options.
- [ ] Implement prompts.
- [ ] Implement console output.
- [ ] Implement exit codes.
- [ ] Implement `--version`.
- [ ] Implement `help`.
- [ ] Implement CLI tests.

---

## Phase 3 — Golden Template

- [ ] Create ASP.NET project template.
- [ ] Create Vue project.
- [ ] Configure Vite.
- [ ] Configure TypeScript.
- [ ] Configure shadcn-vue.
- [ ] Configure TanStack Query.
- [ ] Configure Pinia.
- [ ] Configure VeeValidate.
- [ ] Configure Zod.
- [ ] Configure tests.
- [ ] Configure Dockerfile.
- [ ] Create `dotisan.config`.

---

## Phase 4 — `dotisan new`

- [ ] Create quick wizard.
- [ ] Add database selection.
- [ ] Add auth selection.
- [ ] Add registration selection.
- [ ] Add tenancy selection.
- [ ] Add package-manager selection.
- [ ] Add advanced mode.
- [ ] Generate solution.
- [ ] Restore dependencies.
- [ ] Validate generated project builds.

---

## Phase 5 — Endpoint Contract

- [ ] Define endpoint marker.
- [ ] Define conventions.
- [ ] Define metadata.
- [ ] Define Request convention.
- [ ] Define Response convention.
- [ ] Define Handler convention.
- [ ] Define Validator convention.
- [ ] Add test endpoints.

---

## Phase 6 — Source Generator

- [ ] Discover endpoints.
- [ ] Generate route registration.
- [ ] Generate handler registration.
- [ ] Generate validators.
- [ ] Add compile diagnostics.
- [ ] Test deterministic output.

---

## Phase 7 — Manifest

- [ ] Define manifest schema.
- [ ] Generate manifest.
- [ ] Add version.
- [ ] Add hash.
- [ ] Test deterministic output.

---

## Phase 8 — FluentValidation

- [ ] Run nested validators.
- [ ] Generate filter registration.
- [ ] Generate ProblemDetails.
- [ ] Generate validation metadata.
- [ ] Add tests.

---

## Phase 9 — EF Core

- [ ] SQLite baseline.
- [ ] DbContext.
- [ ] provider abstraction only where required for scaffolding.
- [ ] entity registration.
- [ ] EF integration tests.

---

## Phase 10 — Scaffolding

- [ ] `make:resource`.
- [ ] `make:endpoint`.
- [ ] generate CRUD files.
- [ ] register model.
- [ ] generate tests.
- [ ] do not generate migration.

---

## Phase 11 — Migrations

- [ ] `dotisan migrate`.
- [ ] development migration generation.
- [ ] apply.
- [ ] production behavior.
- [ ] status.
- [ ] rollback.
- [ ] migration tests.

---

## Phase 12 — OpenAPI

- [ ] first-party ASP.NET OpenAPI.
- [ ] manifest metadata.
- [ ] raw OpenAPI.
- [ ] Scalar integration.
- [ ] Dotisan branding.
- [ ] dev-only default.

---

## Phase 13 — TypeScript

- [ ] primitive mapping.
- [ ] request models.
- [ ] response models.
- [ ] numeric enums.
- [ ] dates.
- [ ] nullable.
- [ ] optional.
- [ ] shared contracts.
- [ ] deterministic files.

---

## Phase 14 — Zod

- [ ] required fields.
- [ ] length.
- [ ] numbers.
- [ ] email.
- [ ] regex.
- [ ] enums.
- [ ] nullable/optional.

---

## Phase 15 — API Client

- [ ] fetch wrapper.
- [ ] JSON.
- [ ] ProblemDetails.
- [ ] cancellation.
- [ ] generated services.
- [ ] tests.

---

## Phase 16 — TanStack Query

- [ ] query keys.
- [ ] list.
- [ ] detail.
- [ ] create.
- [ ] update.
- [ ] delete.
- [ ] invalidation.

---

## Phase 17 — Vue Foundation

- [ ] layouts.
- [ ] routing.
- [ ] breadcrumbs.
- [ ] state components.
- [ ] tables.
- [ ] toasts.
- [ ] theme.
- [ ] accessibility baseline.

---

## Phase 18 — CRUD Scaffolding

- [ ] list page.
- [ ] create form.
- [ ] edit form.
- [ ] detail page.
- [ ] delete flow.
- [ ] route registration.
- [ ] optional navigation entry.

---

## Phase 19 — `dotisan dev`

- [ ] process supervisor.
- [ ] API.
- [ ] Vite.
- [ ] Aspire.
- [ ] container infrastructure.
- [ ] log output.
- [ ] shutdown.
- [ ] interactive controls.
- [ ] CLI overrides.

---

## Phase 20 — OpenTelemetry

- [ ] ASP.NET instrumentation.
- [ ] EF instrumentation.
- [ ] HttpClient instrumentation.
- [ ] logging.
- [ ] Aspire export.
- [ ] OTLP support.

---

# Initial Implementation Boundary

Do **not** implement authentication, tenancy, queues, imports, billing, compliance, or the other later batteries until the following workflow is excellent:

```text
dotisan new MyApp
dotisan make:resource Customer
dotisan migrate
dotisan dev
```

That workflow is the first proof that Dotisan solves the problem it exists to solve.

After that foundation is stable, extend the Spec Kit with separate feature specifications such as:

```text
002-authentication
003-authorization
004-audit
005-jobs
006-scheduler
007-notifications
008-storage
009-multitenancy
010-import-export
...
```

Each should go through its own:

```text
spec
→ plan
→ tasks
→ implement
```

rather than turning `001-dotisan-framework` into one unimplementable mega-feature.