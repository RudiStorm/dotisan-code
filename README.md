# Dotisan

Dotisan is a .NET 10 application scaffolding CLI for building ordinary ASP.NET Core and Vue applications. It gives you a working API, frontend, database wiring, typed frontend contracts, optional authentication, resource scaffolding, and durable background jobs without hiding the generated source code behind a proprietary runtime.

The current prerelease is v0.9.0-beta.1. It is intended for evaluation and early adopter feedback; production users should wait for the stable 0.9.0 release.

## What you get

`dotisan new` creates a full application workspace containing:

- An ASP.NET Core 10 Minimal API.
- A Vue 3 + Vite frontend written in TypeScript.
- EF Core data access with SQLite by default, or SQL Server, PostgreSQL, or MySQL.
- Explicit endpoint registration and deterministic API contract metadata.
- Generated OpenAPI, TypeScript models, Zod schemas, fetch services, and TanStack Query helpers.
- Optional ASP.NET Core Identity cookie authentication and role-claim authorization.
- Wolverine-backed durable local queues, delayed messages, recurring sample scheduling, and retries.
- Opt-in provider-neutral notifications, local file storage, caching, CSV/JSON imports and exports, and signed webhooks. See [integration readiness](docs/integrations-readiness.md) before promoting optional capabilities to production.
- A solution file, `dotisan.config`, appsettings, local development defaults, and editable source boundaries.

Dotisan owns the initial scaffold. After that, the generated application is yours: edit the files under `src/<Name>.Api` and `src/<Name>.Web`, use standard .NET and frontend tooling, and keep generated contract output refreshed with `dotisan generate`.

## Install the CLI

Install the .NET tool from NuGet:

```powershell
dotnet tool install --global Dotisan --version 0.9.0-beta.1
dotisan --version
```

For a locally built package:

```powershell
dotnet tool install --global Dotisan --add-source .\artifacts --version 0.9.0-beta.1
```

Upgrade an existing installation with:

```powershell
dotnet tool update --global Dotisan --version 0.9.0-beta.1
```

## Create and run an application

The shortest useful workflow is:

```powershell
dotisan new MyApp
cd .\MyApp
dotisan dev
```

The wizard asks for the project name, database provider, authentication, registration policy, and frontend package manager. To use explicit options and skip prompts:

```powershell
dotisan new MyApp --yes --output .\MyApp
cd .\MyApp
dotisan dev
```

`dotisan dev` runs the API and Vue development server together. The API listens on `http://localhost:5000`; Vite proxies `/api` calls to it. Press Ctrl+C once to stop both processes gracefully.

Useful development variants:

```powershell
dotisan dev --lean                 # API only
dotisan dev --environment Staging  # use a different ASP.NET Core environment
dotisan dev --observability        # enable OTLP export when generated with --observability yes
dotisan run                        # run only the API with dotnet run
dotisan build                      # build the API and frontend
dotisan build --no-frontend        # build only the .NET solution
```

For external database providers, `dotisan dev` starts the generated Docker Compose `database` service, waits for its health check, and stops the container when development ends. The named database volume is preserved.

## Observability

Generated applications include OpenTelemetry tracing and metrics for ASP.NET Core and HTTP client activity. Telemetry collection is registered by default, while exporting is opt-in:

```powershell
$env:OpenTelemetry__Enabled = "true"
$env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:4317"
dotnet run --project src\MyApp.Api
```

Or use `dotisan dev --observability`, which enables OTLP export for the coordinated API process. Standard OpenTelemetry environment variables remain the production configuration boundary; Dotisan does not select a vendor-specific backend.

With `--observability`, `dotisan dev` also starts the generated Aspire Dashboard at `http://localhost:18888` and maps OTLP on ports `4317` and `4318`. The dashboard is a development service only.

Generated Vue projects include Vitest, Vue Test Utils, Playwright, Tailwind, and shadcn-vue-compatible `components.json` configuration. Run `pnpm test`, `pnpm run build`, and `pnpm run test:e2e` from the frontend directory.

## Command reference

### Project creation

```text
dotisan new <ProjectName> [options]
```

Common options include:

```text
--yes                         accept defaults without prompting
--output <directory>          choose the destination directory
--database <sqlite|sqlserver|postgresql|mysql>
--auth <yes|no>               enable or disable Identity authentication
--registration <public|invite-only|disabled>
--package-manager <npm|pnpm>
--no-restore                  generate files without restoring the solution
```

`dotisan new` restores the generated .NET solution and installs frontend dependencies when requested. `dotisan dev` creates an `InitialCreate` migration when a generated project has none and applies local development migrations automatically. Production migrations remain explicit: author and review them with `dotnet ef migrations add`, then apply committed migrations with `dotisan migrate`. Use `--no-restore` for intentional offline generation; run `dotnet restore` and the selected package manager install yourself before building. The generated Dockerfiles require the corresponding lockfile (`package-lock.json` for npm or `pnpm-lock.yaml` for pnpm) and use frozen installs.

### Development and build

```text
dotisan dev [--lean] [--observability] [--environment <name>]
dotisan run
dotisan build [--no-frontend]
dotisan doctor [--production]
dotisan add:integration <name> [--dry-run]
dotisan remove:integration <name> --force
```

Use `dev` for the coordinated API/frontend development experience, `run` for the API alone, `build` for a production-style compile of the generated projects, and `doctor --production` for read-only readiness diagnostics.

Integration commands create or remove only reviewable recipe files under `.dotisan/integrations`. They do not install opaque runtime plugins or delete hand-edited source.

### Frontend contracts

```text
dotisan generate
dotisan generate --check
dotisan generate --no-openapi
```

`dotisan generate` builds the solution, reads the generated endpoint contract, and refreshes these files:

```text
src/<Name>.Web/src/dotisan/features/<feature>/models.ts
src/<Name>.Web/src/dotisan/features/<feature>/schemas.ts
src/<Name>.Web/src/dotisan/features/<feature>/services.ts
src/<Name>.Web/src/dotisan/features/<feature>/queries.ts
src/<Name>.Web/src/dotisan/openapi.json
```

Each feature also has an `index.ts`, and the root `models.ts`, `schemas.ts`, `services.ts`, and `queries.ts` files are compatibility barrels. Use `dotisan generate --check` in CI to detect stale generated output. Use `--no-openapi` when only the TypeScript contract files are needed.

### Database migrations

```text
dotisan migrate
dotisan migrate status
dotisan migrate --production
```

`dotisan migrate` applies existing EF Core migrations and is the explicit production schema boundary. Migration authoring and review remain explicit developer actions:

```powershell
dotnet ef migrations add InitialCreate --project src\MyApp.Api
dotisan migrate
```

To roll back, use standard EF Core tooling after reviewing the target migration:

```powershell
dotnet ef database update <MigrationName> --project src\MyApp.Api
```

Review generated migrations before applying them. For authenticated projects, use an Identity migration such as `InitialIdentity`; for audit support, use an audit migration such as `InitialAudit`.

### Resource and endpoint scaffolding

```text
dotisan make:resource Customer
dotisan make:endpoint HealthCheck
dotisan make:crud Customer
```

`make:resource` creates an editable model, EF `DbSet`, and API resource endpoints. `make:endpoint` creates a single-file vertical endpoint. `make:crud` creates Vue list/detail/form and route files outside the generated contract-output directory.

Scaffolding does not create migrations. After changing the data model, author and review a migration with `dotnet ef migrations add`.

### Jobs and scheduling inspection

```text
dotisan jobs status
dotisan schedule list
```

`jobs status` reports whether generated jobs are enabled, the selected persistence provider, and the registration source path. `schedule list` reads explicit `DOTISAN:SCHEDULE` declarations from `Jobs/JobRegistration.cs` and shows the source file to edit. These commands inspect source conventions; they do not rely on private runtime tables.

## Generated application layout

```text
MyApp/
├── MyApp.sln
├── dotisan.config
├── src/
│   ├── MyApp.Api/
│   │   ├── Data/
│   │   ├── Features/
│   │   ├── Infrastructure/
│   │   ├── Jobs/
│   │   └── Program.cs
│   └── MyApp.Web/
│       └── src/
│           ├── components/
│           ├── features/
│           ├── layouts/
│           ├── pages/
│           ├── routes/
│           └── dotisan/
└── tests/
    └── MyApp.Api.Tests/
```

The API uses standard ASP.NET Core dependency injection, configuration, logging, Minimal APIs, EF Core, and middleware. The frontend uses standard Vue, Vite, TypeScript, Pinia, Vue Router, Zod, and TanStack Query. You can replace or extend these pieces with normal ecosystem tooling.

## Durable jobs and scheduling

The generated API integrates Wolverine for background work:

- `Jobs/SampleJob.cs` contains an editable message contract.
- `Jobs/SampleJobHandler.cs` contains an editable handler with DI and logging.
- `Features/Jobs/JobEndpoints.cs` demonstrates immediate and delayed dispatch.
- `Jobs/JobRegistration.cs` configures durable local queues, provider-specific message persistence, EF Core transactions, retries, and the recurring sample starter.

The default configuration is:

```json
{
  "Dotisan": {
    "Jobs": {
      "Enabled": true,
      "MaxAttempts": 3,
      "RetryDelaySeconds": 5
    }
  }
}
```

Override configuration through normal ASP.NET Core providers, for example:

```powershell
$env:Dotisan__Jobs__Enabled = "false"
$env:Dotisan__Jobs__MaxAttempts = "5"
$env:Dotisan__Jobs__RetryDelaySeconds = "10"
dotisan dev
```

Jobs use the same `DefaultConnection` as the application. SQLite uses a file-backed store by default. Wolverine’s storage schema must exist before durable messages can be used; manage it explicitly with Wolverine’s native resource tooling or the provider workflow appropriate to your deployment. Failed messages remain available through Wolverine’s native error-handling and replay tooling.

## Authentication and authorization

Create an authenticated application with:

```powershell
dotisan new AuthApp --auth yes --registration public
```

Authenticated projects use standard ASP.NET Core Identity, EF Core stores, cookie authentication, antiforgery, and ProblemDetails. They include registration, login, logout, current-user, email-confirmation, password-reset, TOTP MFA/recovery-code, and session/device endpoints. Login returns `mfa_required` when a second factor is needed, and the generated Vue portal includes setup, challenge, and session-management screens. Password-reset requests are anti-enumeration safe and use the generated mail-provider selector: `console`, `mailpit`, or `smtp`. Select Mailpit with `dotisan new AuthApp --auth yes --mail-provider mailpit`; this generates Compose service ports `1025` and `8025`, and `dotisan dev` starts it automatically. Staging and production can select `smtp` through `Mail__Provider` and standard `Mail__Smtp__*` configuration.

Outside Development, generated APIs fail fast unless `Dotisan__Security__FrontendUrl` is an absolute HTTPS URL and `Dotisan__Security__DataProtectionKeyDirectory` points to persistent storage. Configure `Dotisan__Security__KnownProxies` only with trusted proxy IPs. Authenticated production environments must use SMTP or a custom `IEmailProvider`; console mail and Mailpit are development-only. Store SMTP credentials, data-protection keys, database credentials, and provider secrets in Secret Manager or the deployment platform's secret store.

When Mailpit is selected, use `dotisan mail` to print the local inbox URL or `dotisan mail --open` to open it in a browser. Mailpit is Development-only; generated APIs reject `Mail:Provider=mailpit` in other environments. Use SMTP for staging and production and keep credentials in deployment secrets. `Integrations/IntegrationExamples.cs` contains optional SendGrid, Mailgun, and configuration-driven OAuth2 examples; register only the adapter you have configured through deployment secrets. External login registration, linking, callback, and unlinking are exposed through the provider-neutral `IExternalLoginProvider` contract.

Before starting the app, author and apply the Identity schema:

```powershell
dotnet ef migrations add InitialIdentity --project src\AuthApp.Api
dotisan migrate
dotisan dev
```

The generated authorization foundation uses code-defined permission constants and standard Identity role claims. Authenticated projects also generate an `/admin/authorization` Vue screen and protected APIs for listing users/roles and assigning roles. Access requires the `authorization.manage` permission claim. Generated protected endpoints return `401` for unauthenticated callers and `403` for authenticated callers without the required permission claim. Edit `src\AuthApp.Api\Authorization\Permissions.cs` and the generated endpoint files directly.

## Database providers

SQLite is the default and needs no external server. SQL Server, PostgreSQL, and MySQL projects include a provider-specific `compose.yaml` with a health check and named volume:

```powershell
docker compose up -d --wait --wait-timeout 120 database
dotnet ef migrations add InitialCreate --project src\MyApp.Api
dotisan migrate
```

Replace development credentials through standard ASP.NET Core configuration or user secrets. Do not commit production secrets, connection strings, SQLite files, or data-protection keys.

## Troubleshooting

Check the local prerequisites:

```powershell
dotnet --version
dotnet ef --version
npm --version       # or pnpm --version
docker compose version
```

Common fixes:

- If `dotisan new` reports a missing tool, install the tool and rerun the relevant native command. Generation still completes where possible.
- If package restore fails, run `dotnet restore <Name>.sln` from the generated project directory.
- If frontend dependencies are missing, run `npm install` or `pnpm install` from `src/<Name>.Web`.
- If generated contract files are stale, run `dotisan generate`.
- If an external database is unhealthy, inspect it with `docker compose ps` and `docker compose logs database --tail 100`.
- If registration fails with `SQLite Error 1: 'no such table: AspNetUsers'`, stop the API and run `dotisan dev` once so the initial development migration is created and applied.
- If migrations are missing, author them explicitly with `dotnet ef migrations add <Name>` before running `dotisan migrate`.
- If durable jobs fail during startup, confirm the connection string is valid and the Wolverine storage schema has been provisioned.

## Contributing and repository development

The repository itself targets .NET 10 and contains the CLI, core contracts, generators, ASP.NET integration, source generators, TypeScript generation, and tests. From the repository root:

```powershell
dotnet restore Dotisan.sln
dotnet build Dotisan.sln --configuration Release
dotnet test Dotisan.sln --configuration Release
dotnet pack src\Dotisan.Cli\Dotisan.Cli.csproj --configuration Release --output .\artifacts
```

The package is a .NET global tool with the command name `dotisan` and is licensed under MIT.

### NuGet signing

The v0.9.0-beta.1 package is currently unsigned. NuGet signing is not required for this prerelease build or artifact validation, and no signing certificate or publishing secret is included in this repository. If the project adopts a signed-package policy for public releases, configure certificate-based signing in the protected publishing workflow; never commit the certificate or its password to source control.

### Beta feedback and promotion

Please use the GitHub issue templates to report installation, generation, or runtime problems against the `0.9.0-beta.1` package. The stable 0.9.0 release will follow only after the published-package clean-machine smoke test and generated-project release gates pass unchanged with beta feedback addressed.
