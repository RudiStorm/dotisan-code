# Dotisan

Dotisan is a batteries-included application framework for standard ASP.NET Core and Vue applications. The v0.4.0 release provides a testable CLI foundation, authentication/authorization primitives, an inspectable audit foundation, and a deterministic golden template.

## v0.4.0 workflow and audit foundation

The repository contains focused projects for:

- Dotisan.Cli — command registration, help/version, prompts, console abstraction, exit codes, project creation, scaffolding, migrations, builds, and development supervision.
- Dotisan.Core — project options, endpoint marker/manifest contracts, and shared CLI contracts.
- Dotisan.AspNetCore — explicit endpoint mapping helpers built on Minimal APIs.
- Dotisan.Generators — inspectable ASP.NET Core + Vue/Vite golden template with configurable EF Core providers and vertical resource/endpoint scaffolding.
- Dotisan.SourceGenerators — Roslyn-generated explicit endpoint registration, DI wiring, and manifest source.
- Dotisan.TypeScript — initial nullable/optional C# contract type mapping.
- Dotisan.Testing — reusable test helpers.

The generated app uses normal ASP.NET Core configuration, dependency injection, Minimal APIs, and EF Core. SQLite is the default provider; the Quick wizard can also generate SQL Server, PostgreSQL, or MySQL configuration. When selected, ASP.NET Core Identity uses the same generated provider and standard cookie authentication. Dotisan does not replace those platform features.

## Run locally

~~~powershell
dotnet restore Dotisan.sln
dotnet build Dotisan.sln
dotnet test Dotisan.sln
dotnet run --project src\Dotisan.Cli -- --version
dotnet run --project src\Dotisan.Cli -- help
~~~

Create a project without interactive input:

~~~powershell
dotnet run --project src\Dotisan.Cli -- new MyApp --yes --output .\MyApp
dotnet build .\MyApp\MyApp.sln

dotnet run --project src\Dotisan.Cli -- new AuthApp --auth yes --registration public --yes --output .\AuthApp
dotnet build .\AuthApp\AuthApp.sln

cd .\MyApp
dotisan make:resource Customer
dotisan migrate
dotisan dev
~~~

`dotisan new` restores the generated .NET solution and runs the selected frontend package manager's install command before it reports the project ready. Use `--no-restore` only when you intentionally need offline generation; run `dotnet restore` and `npm install` or `pnpm install` manually afterward.

After the wizard choices are accepted, `dotisan new` checks the .NET SDK, `dotnet-ef`, and the selected frontend package manager. Missing tools are reported with copy-paste install and verification commands. Project creation continues so you can install the missing prerequisite and retry the relevant native command; a missing npm/pnpm installation skips only frontend dependency installation.

Press Ctrl+C once while `dotisan dev` is running to stop the API and frontend together. Dotisan gives each service a short graceful-shutdown window and uses process-tree cleanup only if a service does not exit.

For the interactive Quick wizard, omit --yes. Each menu is numbered, so you can enter `1`, `2`, or another displayed number instead of typing the full choice. It asks for SQLite/SQL Server/PostgreSQL/MySQL, authentication, registration policy, tenancy, and pnpm/npm. Press Enter to accept each default; the original text choices remain supported. Authentication is opt-in; `--auth yes` generates standard ASP.NET Core Identity endpoints and cookie authentication.

## Database providers

SQLite is the default and uses `Data Source=app.db`, so a new project can run without a separate database service. The other wizard choices generate the matching EF Core provider package, `UseSqlServer`, `UseNpgsql`, or `UseMySql` registration, and a local development connection-string example:

- SQL Server: `Server=localhost,1433;Database=MyApp;User Id=sa;Password=DotisanDev123!;TrustServerCertificate=True`
- PostgreSQL: `Host=localhost;Database=myapp;Username=postgres;Password=postgres`
- MySQL: `Server=localhost;Database=myapp;User=root;Password=root`

For external providers, `dotisan new` generates a provider-specific `compose.yaml` with a health check and named data volume. Keep Docker Desktop running; `dotisan dev` starts the `database` service before the API and frontend, waits for readiness for up to 120 seconds, and stops the container on exit without deleting the volume. You can start it manually with `docker compose up -d --wait --wait-timeout 120 database`. If Compose reports that the database is unhealthy, inspect it with `docker compose ps`, `docker compose logs database --tail 100`, and `docker inspect (docker compose ps -q database) --format '{{json .State.Health}}'`. Replace development credentials through normal ASP.NET Core configuration or user secrets before authoring and applying migrations. Production database hosting remains your deployment responsibility. Use standard EF Core tooling:

~~~powershell
dotnet ef migrations add InitialCreate --project src\MyApp.Api
dotnet ef database update --project src\MyApp.Api
~~~

## Global tool packaging

~~~powershell
dotnet pack src\Dotisan.Cli\Dotisan.Cli.csproj --configuration Release --output .\artifacts
dotnet tool install --global Dotisan --add-source .\artifacts --version 0.4.0
dotisan new MyApp
~~~

`make:resource` creates an editable model, EF `DbSet`, list/create Minimal API endpoint, and explicit registration. It does not create migrations; use `dotisan migrate` only after reviewing and authoring migrations with normal `dotnet ef` tooling. For an authenticated project, author the initial Identity migration with `dotnet ef migrations add InitialIdentity --project src\AuthApp.Api`.

Authentication projects use normal ASP.NET Core Identity, cookie authentication, antiforgery, and ProblemDetails. Configure production connection strings and secrets through standard ASP.NET Core providers, serve over HTTPS, and configure durable data-protection keys when running more than one instance.

Authenticated projects also generate an explicit authorization foundation. Edit `src\<Name>.Api\Authorization\Permissions.cs` for code-defined permission names, use standard `IdentityRole` role claims with claim type `permission`, and protect generated endpoints with explicit `RequireAuthorization(...)` calls. The generated profile probe demonstrates `profile.view`; generated resources add `<Resource>View`, `<Resource>Create`, `<Resource>Update`, and `<Resource>Delete` permissions. API callers receive `401` when unauthenticated and `403` when authenticated without the required role claim.

The v0.4.0 audit foundation generates an editable `AuditEntry` model and scoped `IAuditWriter` service for plain and authenticated projects. Auditing is enabled by default through standard `Audit:Enabled` configuration; set `Audit__Enabled=false` to disable writes. Generated resource and authentication operations record actor, resource, action, changed fields, trace ID, and correlation ID. Author the schema with `dotnet ef migrations add InitialAudit`; audit is persistence logging, not event sourcing.

## Deliberately deferred commands

Admin authorization UI/API, tenancy, jobs, observability, integrations, UI CRUD generation, audit query UI/API, and production diagnostics remain subsequent feature specifications. Password reset, email confirmation, MFA, and external providers are not generated. Role and permission assignment remains ordinary application code using ASP.NET Core Identity; Dotisan does not add a runtime permission registry or admin surface. `add`, `remove`, and `doctor` intentionally return a helpful exit code 3 until those specifications are implemented.

## Project direction

See Dotisan Spec Kit.md for the constitution, architecture, contracts, and staged roadmap. See docs/quickstart.md for the short developer workflow.
