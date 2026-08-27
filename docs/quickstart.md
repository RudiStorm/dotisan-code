# Dotisan Development Quickstart

## Create an application

From this repository:

~~~powershell
dotnet run --project src\Dotisan.Cli -- new TodoApp
~~~

Choose Quick Setup, then accept the defaults:

~~~text
Database: SQLite
Authentication: No
Multi-tenancy: No
Package manager: pnpm
~~~

All wizard menus show numbered choices. Enter `1`, `2`, or another displayed number to choose quickly; Enter accepts the default, and the text labels remain supported.

SQLite is the default because it is file-based and requires no separate database service. The wizard also supports SQL Server, PostgreSQL, and MySQL. Each choice generates the matching EF Core package, provider registration, and local connection-string example in `src/TodoApp.Api/appsettings.json`.

Authentication is opt-in. To generate the v0.3.0 authorization slice non-interactively:

~~~powershell
dotnet run --project src\Dotisan.Cli -- new AuthApp --auth yes --registration public --yes --output .\AuthApp
~~~

This adds standard ASP.NET Core Identity with EF Core, cookie authentication, antiforgery protection, and explicit account endpoints. Registration can be `public`, `invite-only`, or `disabled`; invite-only and disabled projects return a clear `registration_unavailable` ProblemDetails response until an invitation workflow is added. Authenticated projects also generate `Authorization/Permissions.cs`, a protected `/api/authorization/profile` probe, and explicit policy registration from standard ASP.NET Core authorization.

For scripts and CI, use the non-interactive equivalent:

~~~powershell
dotnet run --project src\Dotisan.Cli -- new TodoApp --yes --output .\TodoApp
~~~

Project creation restores the .NET solution and installs frontend dependencies with the selected package manager before reporting success. Use `--no-restore` for intentional offline generation; then run `dotnet restore` and `npm install` or `pnpm install` yourself.

Before restore and frontend installation, Dotisan checks that the .NET SDK, `dotnet-ef`, and the selected package manager are available. If a tool is missing, the command prints an install command and a verification command. The project is still created; a missing npm or pnpm only skips frontend installation until that tool is installed.

Common prerequisite commands:

~~~powershell
dotnet tool install --global dotnet-ef
npm install --global pnpm # only when pnpm was selected and is missing
~~~

Install Node.js LTS from https://nodejs.org/ when npm is missing. Verify with `dotnet ef --version`, `npm --version`, or `pnpm --version` as appropriate.

The generated project contains:

~~~text
TodoApp/
├── src/TodoApp.Api/
│   ├── Data/AppDbContext.cs
│   ├── Infrastructure/DotisanEndpointExtensions.cs
│   ├── Program.cs
│   └── appsettings.json
├── src/TodoApp.Web/
│   ├── src/
│   ├── package.json
│   └── vite.config.ts
├── tests/TodoApp.Api.Tests/
├── dotisan.config
├── Directory.Packages.props
├── Dockerfile
└── TodoApp.sln
~~~

## Database providers

For SQL Server, PostgreSQL, or MySQL, keep Docker Desktop running. `dotisan new` checks Docker and generates a provider-specific `compose.yaml`; `dotisan dev` starts the `database` service, waits for its health check for up to 120 seconds, and stops the container on exit without deleting its named volume. You can start it manually with `docker compose up -d --wait --wait-timeout 120 database`. If the service becomes unhealthy, inspect `docker compose ps`, `docker compose logs database --tail 100`, and `docker inspect (docker compose ps -q database) --format '{{json .State.Health}}'`. Configure credentials with normal ASP.NET Core configuration or user secrets rather than committing them. Production database hosting remains your deployment responsibility.

The generated defaults are:

~~~text
SQLite:     Data Source=app.db
SQL Server: Server=localhost,1433;Database=TodoApp;User Id=sa;Password=DotisanDev123!;TrustServerCertificate=True
PostgreSQL: Host=localhost;Database=todoapp;Username=postgres;Password=postgres
MySQL:      Server=localhost;Database=todoapp;User=root;Password=root
~~~

The API uses `UseSqlite`, `UseSqlServer`, `UseNpgsql`, or `UseMySql` according to the wizard choice. Author and apply migrations with the standard EF Core CLI:

~~~powershell
dotnet ef migrations add InitialCreate --project src\TodoApp.Api
dotnet ef database update --project src\TodoApp.Api
~~~

## Build and run

~~~powershell
cd TodoApp
dotnet build TodoApp.sln
dotnet run --project src\TodoApp.Api
~~~

Dotisan can run the API and frontend together from the project root:

~~~powershell
dotisan dev
dotisan dev --lean
dotisan build
~~~

Press Ctrl+C once to stop `dotisan dev`. The CLI coordinates shutdown for both services, then falls back to process-tree cleanup if a child does not exit gracefully.

## Create a resource

~~~powershell
dotisan make:resource TodoItem
dotnet build TodoApp.sln
dotnet ef migrations add AddTodoItem --project src\TodoApp.Api
dotisan migrate
~~~

The scaffold is ordinary source code and remains yours to edit. `make:resource` never creates a migration automatically.

In another terminal, run the Vue app. Dependencies were installed by `dotisan new`:

~~~powershell
cd TodoApp\src\TodoApp.Web
pnpm dev
~~~

npm run dev is supported as an alternative. If the project was generated with `--no-restore`, run `npm install` or `pnpm install` first.

## Native tooling stays available

The generated app is ordinary ASP.NET Core + EF Core + Vue/Vite source. You can use:

~~~powershell
dotnet ef migrations add InitialCreate --project src\TodoApp.Api
dotnet ef database update --project src\TodoApp.Api
dotnet watch --project src\TodoApp.Api
~~~

`dotisan migrate` applies existing EF Core migrations. `dotisan migrate status` lists migrations. Migration authoring and rollback remain available through standard `dotnet ef` commands.

## Audit foundation

Generated projects include `Auditing/AuditEntry.cs`, `Auditing/IAuditWriter.cs`, and `Auditing/AuditWriter.cs`. Audit writes are enabled by default through standard ASP.NET Core configuration:

~~~powershell
dotnet ef migrations add InitialAudit --project src\TodoApp.Api
dotnet ef database update --project src\TodoApp.Api
$env:Audit__Enabled = "false" # optional local/deployment override
~~~

Generated resource operations and authenticated security operations record actor, tenant placeholder, timestamp, resource/resource ID, action, changed fields, trace ID, and correlation ID. User-written endpoints opt in by injecting `IAuditWriter`. Audit is persistence logging, not event sourcing, and no audit query/admin UI is generated.

## Authentication projects

For an authenticated project, create and apply the initial Identity schema with standard EF Core tooling:

~~~powershell
dotnet ef migrations add InitialIdentity --project src\AuthApp.Api
dotnet ef database update --project src\AuthApp.Api
dotisan migrate
~~~

`dotisan migrate` applies existing migrations; it does not create migrations. Keep production secrets and data-protection keys outside source control, use HTTPS, and configure a durable key ring for multi-instance deployments through normal ASP.NET Core configuration. Password reset, email confirmation, MFA, and external providers remain deferred. Edit `src\AuthApp.Api\Authorization\Permissions.cs` for permission constants; assign those permission claims to standard `IdentityRole` instances with `RoleManager<IdentityRole>`. Protected APIs return `401` without a session and `403` without the required `permission` role claim.
