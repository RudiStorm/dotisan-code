# Dotisan

Dotisan is a batteries-included application framework for standard ASP.NET Core and Vue applications. The first implementation slice provides a testable CLI foundation and a deterministic golden template.

## Current slice

The repository contains focused projects for:

- Dotisan.Cli — command registration, help/version, prompts, console abstraction, exit codes, and dotisan new.
- Dotisan.Core — project options, endpoint marker/manifest contracts, and shared CLI contracts.
- Dotisan.AspNetCore — explicit endpoint mapping helpers built on Minimal APIs.
- Dotisan.Generators — inspectable ASP.NET Core + Vue/Vite + SQLite golden template.
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
~~~

For the interactive Quick wizard, omit --yes. It asks for SQLite/SQL Server/PostgreSQL/MySQL, authentication, registration policy, tenancy, and pnpm/npm. Press Enter to accept each default.

## Global tool packaging

~~~powershell
dotnet pack src\Dotisan.Cli\Dotisan.Cli.csproj --configuration Release --output .\artifacts
dotnet tool install --global Dotisan --add-source .\artifacts --version 0.1.0
dotisan new MyApp
~~~

## Deliberately deferred commands

make:resource, make:endpoint, migrate, dev, build, run, add, remove, and doctor are registered so the CLI can give explicit guidance. They currently return exit code 3 and explain the native .NET tooling to use while each feature is implemented.

## Project direction

See Dotisan Spec Kit.md for the constitution, architecture, contracts, and staged roadmap. See docs/quickstart.md for the short developer workflow.
