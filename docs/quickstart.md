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

Authentication is opt-in. To generate the v0.2 authentication slice non-interactively:

~~~powershell
dotnet run --project src\Dotisan.Cli -- new AuthApp --auth yes --registration public --yes --output .\AuthApp
~~~

This adds standard ASP.NET Core Identity with EF Core, cookie authentication, antiforgery protection, and explicit account endpoints. Registration can be `public`, `invite-only`, or `disabled`; invite-only and disabled projects return a clear `registration_unavailable` ProblemDetails response until an invitation workflow is added.

For scripts and CI, use the non-interactive equivalent:

~~~powershell
dotnet run --project src\Dotisan.Cli -- new TodoApp --yes --output .\TodoApp
~~~

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

## Create a resource

~~~powershell
dotisan make:resource TodoItem
dotnet build TodoApp.sln
dotnet ef migrations add AddTodoItem --project src\TodoApp.Api
dotisan migrate
~~~

The scaffold is ordinary source code and remains yours to edit. `make:resource` never creates a migration automatically.

In another terminal, install and run the Vue app:

~~~powershell
cd TodoApp\src\TodoApp.Web
pnpm install
pnpm dev
~~~

npm install and npm run dev are supported as an alternative.

## Native tooling stays available

The generated app is ordinary ASP.NET Core + EF Core + Vue/Vite source. You can use:

~~~powershell
dotnet ef migrations add InitialCreate --project src\TodoApp.Api
dotnet ef database update --project src\TodoApp.Api
dotnet watch --project src\TodoApp.Api
~~~

`dotisan migrate` applies existing EF Core migrations. `dotisan migrate status` lists migrations. Migration authoring and rollback remain available through standard `dotnet ef` commands.

## Authentication projects

For an authenticated project, create and apply the initial Identity schema with standard EF Core tooling:

~~~powershell
dotnet ef migrations add InitialIdentity --project src\AuthApp.Api
dotnet ef database update --project src\AuthApp.Api
dotisan migrate
~~~

`dotisan migrate` applies existing migrations; it does not create migrations. Keep production secrets and data-protection keys outside source control, use HTTPS, and configure a durable key ring for multi-instance deployments through normal ASP.NET Core configuration. Password reset, email confirmation, MFA, external providers, roles, and permissions are intentionally deferred.
