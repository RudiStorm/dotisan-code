# Dotisan

Dotisan is a batteries-included application framework for standard ASP.NET Core and Vue applications. The first implementation slice provides a testable CLI foundation and a deterministic golden template.

## v0.1.0 workflow

The repository contains focused projects for:

- Dotisan.Cli — command registration, help/version, prompts, console abstraction, exit codes, project creation, scaffolding, migrations, builds, and development supervision.
- Dotisan.Core — project options, endpoint marker/manifest contracts, and shared CLI contracts.
- Dotisan.AspNetCore — explicit endpoint mapping helpers built on Minimal APIs.
- Dotisan.Generators — inspectable ASP.NET Core + Vue/Vite + SQLite golden template and vertical resource/endpoint scaffolding.
- Dotisan.SourceGenerators — Roslyn-generated explicit endpoint registration, DI wiring, and manifest source.
- Dotisan.TypeScript — initial nullable/optional C# contract type mapping.
- Dotisan.Testing — reusable test helpers.

The generated app uses normal ASP.NET Core configuration, dependency injection, Minimal APIs, and EF Core SQLite. Dotisan does not replace those platform features.

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

cd .\MyApp
dotisan make:resource Customer
dotisan migrate
dotisan dev
~~~

For the interactive Quick wizard, omit --yes. It asks for SQLite/SQL Server/PostgreSQL/MySQL, authentication, registration policy, tenancy, and pnpm/npm. Press Enter to accept each default.

## Global tool packaging

~~~powershell
dotnet pack src\Dotisan.Cli\Dotisan.Cli.csproj --configuration Release --output .\artifacts
dotnet tool install --global Dotisan --add-source .\artifacts --version 0.1.0
dotisan new MyApp
~~~

`make:resource` creates an editable model, EF `DbSet`, list/create Minimal API endpoint, and explicit registration. It does not create migrations; use `dotisan migrate` only after reviewing and authoring migrations with normal `dotnet ef` tooling.

## Deliberately deferred commands

Authentication, authorization, tenancy, jobs, observability, integrations, UI CRUD generation, and production diagnostics remain subsequent feature specifications. `add`, `remove`, and `doctor` intentionally return a helpful exit code 3 until those specifications are implemented.

## Project direction

See Dotisan Spec Kit.md for the constitution, architecture, contracts, and staged roadmap. See docs/quickstart.md for the short developer workflow.
