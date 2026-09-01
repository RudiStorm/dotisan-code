using Dotisan.Core;

namespace Dotisan.Generators;

internal sealed record TemplateFile(string Path, string Content);

internal static class TemplateFiles
{
    public static IReadOnlyList<TemplateFile> Create(ProjectOptions options, string identifier)
    {
        var packageManager = options.PackageManager == PackageManager.Pnpm ? "pnpm@9.15.0" : "npm@10.9.0";
        var packageJson = $$"""
        {
          "name": "{{options.Name.ToLowerInvariant()}}-web",
          "private": true,
          "version": "0.5.0",
          "packageManager": "{{packageManager}}",
          "type": "module",
          "scripts": {
            "dev": "vite",
            "build": "vue-tsc -b && vite build",
            "test": "vitest run"
          },
          "dependencies": {
            "@tanstack/vue-query": "^5.59.0",
            "pinia": "^2.3.0",
            "vue": "^3.5.0",
            "vue-router": "^4.4.0",
            "zod": "^3.23.0"
          },
          "devDependencies": {
            "@types/node": "^22.0.0",
            "@vitejs/plugin-vue": "^5.2.0",
            "@vue/tsconfig": "^0.7.0",
            "typescript": "~5.6.0",
            "vite": "^6.0.0",
            "vitest": "^2.1.0",
            "vue-tsc": "^2.1.0"
          }
        }
        """;

        return
        [
            new(".gitignore", "bin/\nobj/\nwwwroot/\nnode_modules/\ndist/\n*.db\n"),
            new("global.json", "{\n  \"sdk\": {\n    \"version\": \"8.0.100\",\n    \"rollForward\": \"latestMajor\"\n  }\n}\n"),
            new(".node-version", "22\n"),
            new("Directory.Packages.props", PackageVersions()),
            new("README.md", ProjectReadme(options.Name, options.Database, options.AuthenticationEnabled)),
            new("dotisan.config", $$"""
            version: 1
            profile: quick
            database: {{options.Database.ToString().ToLowerInvariant()}}
            authentication: {{(options.AuthenticationEnabled ? "enabled" : "disabled")}}
            multi_tenancy: {{(options.MultiTenancyEnabled ? "enabled" : "disabled")}}
            package_manager: {{options.PackageManager.ToString().ToLowerInvariant()}}
            dev:
              services:
                api: true
                frontend: true
                database: true
                observability: false
                mail: false
                workers: false
            """),
            new("dotisan.contract.json", InitialContractManifest(options.AuthenticationEnabled).ToJson()),
            new("Dockerfile", Dockerfile(options.Name)),
            ..(options.Database == DatabaseProvider.SQLite
                ? Array.Empty<TemplateFile>()
                : new[] { new TemplateFile("compose.yaml", DatabaseCompose(options.Name, options.Database)) }),
            new($"src/{options.Name}.Api/{options.Name}.Api.csproj", ApiProject(options.Name, options.Database, options.AuthenticationEnabled)),
            new($"src/{options.Name}.Api/Program.cs", ApiProgram(identifier, options.Database, options.AuthenticationEnabled, options.Registration == RegistrationPolicy.Public)),
            new($"src/{options.Name}.Api/Infrastructure/DotisanContractExport.cs", ContractExport(options.AuthenticationEnabled)),
            new($"src/{options.Name}.Api/Data/AppDbContext.cs", DbContext(identifier, options.AuthenticationEnabled)),
            new($"src/{options.Name}.Api/Auditing/AuditEntry.cs", AuditEntry(identifier)),
            new($"src/{options.Name}.Api/Auditing/IAuditWriter.cs", AuditWriterContract(identifier)),
            new($"src/{options.Name}.Api/Auditing/AuditWriter.cs", AuditWriter(identifier)),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Identity/ApplicationUser.cs", ApplicationUser(identifier)) }
                : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Authorization/Permissions.cs", Permissions(identifier)) }
                : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Features/Account/AccountEndpoints.cs", AccountEndpoints(identifier, options.Registration == RegistrationPolicy.Public)) }
                : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Features/Authorization/AuthorizationEndpoints.cs", AuthorizationEndpoints(identifier)) }
                : Array.Empty<TemplateFile>()),
            new($"src/{options.Name}.Api/Infrastructure/DotisanEndpointExtensions.cs", EndpointExtensions(identifier)),
            new($"src/{options.Name}.Api/appsettings.json", AppSettings(options.Name, options.Database)),
            new($"src/{options.Name}.Api/appsettings.Development.json", "{\n  \"Logging\": {\n    \"LogLevel\": {\n      \"Default\": \"Information\",\n      \"Microsoft.AspNetCore\": \"Information\"\n    }\n  }\n}\n"),
            new($"src/{options.Name}.Web/package.json", packageJson),
            new($"src/{options.Name}.Web/index.html", WebIndex(options.Name)),
            new($"src/{options.Name}.Web/tsconfig.json", "{\n  \"files\": [],\n  \"references\": [{ \"path\": \"./tsconfig.app.json\" }, { \"path\": \"./tsconfig.node.json\" }]\n}\n"),
            new($"src/{options.Name}.Web/tsconfig.app.json", "{\n  \"extends\": \"@vue/tsconfig/tsconfig.dom.json\",\n  \"include\": [\"src/**/*.ts\", \"src/**/*.tsx\", \"src/**/*.vue\"],\n  \"compilerOptions\": {\n    \"composite\": true,\n    \"tsBuildInfoFile\": \"./node_modules/.tmp/tsconfig.app.tsbuildinfo\",\n    \"strict\": true,\n    \"target\": \"ES2022\",\n    \"lib\": [\"ES2022\", \"DOM\", \"DOM.Iterable\"],\n    \"moduleResolution\": \"Bundler\"\n  }\n}\n"),
            new($"src/{options.Name}.Web/tsconfig.node.json", "{\n  \"compilerOptions\": {\n    \"composite\": true,\n    \"tsBuildInfoFile\": \"./node_modules/.tmp/tsconfig.node.tsbuildinfo\",\n    \"module\": \"ESNext\",\n    \"moduleResolution\": \"Bundler\",\n    \"allowSyntheticDefaultImports\": true,\n    \"target\": \"ES2022\",\n    \"types\": [\"node\"]\n  },\n  \"include\": [\"vite.config.ts\"]\n}\n"),
            new($"src/{options.Name}.Web/vite.config.ts", ViteConfig()),
            new($"src/{options.Name}.Web/src/env.d.ts", "/// <reference types=\"vite/client\" />\n"),
            new($"src/{options.Name}.Web/src/main.ts", MainTs()),
            new($"src/{options.Name}.Web/src/routes/index.ts", RoutesIndex()),
            new($"src/{options.Name}.Web/src/App.vue", AppVue(options.Name)),
            new($"src/{options.Name}.Web/src/style.css", StyleCss()),
            new($"src/{options.Name}.Web/src/generated/.gitkeep", string.Empty),
            new($"tests/{options.Name}.Api.Tests/{options.Name}.Api.Tests.csproj", ApiTestsProject(options.Name, options.AuthenticationEnabled)),
            new($"tests/{options.Name}.Api.Tests/HealthEndpointTests.cs", ApiTests(identifier)),
            new($"tests/{options.Name}.Api.Tests/Usings.cs", "global using Xunit;\n"),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"tests/{options.Name}.Api.Tests/AuthenticationEndpointTests.cs", AuthenticationTests(identifier, options.Registration)) }
                : Array.Empty<TemplateFile>()),
            new($"{options.Name}.sln", Solution(options.Name))
        ];
    }

    private static string ApiProject(string name, DatabaseProvider database, bool authenticationEnabled) => $$"""
    <Project Sdk="Microsoft.NET.Sdk.Web">
      <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <RootNamespace>{{name.Replace('-', '_')}}</RootNamespace>
        <DefineConstants>DOTISAN_CONTRACT_FALLBACK</DefineConstants>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="{{DatabasePackage(database)}}" />
        <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
        <PackageReference Include="Microsoft.EntityFrameworkCore.Design" PrivateAssets="all" />
        {{(authenticationEnabled ? "<PackageReference Include=\"Microsoft.AspNetCore.Identity.EntityFrameworkCore\" />" : string.Empty)}}
      </ItemGroup>
    </Project>
    """;

    private static string PackageVersions() => """
    <Project>
      <PropertyGroup>
        <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
      </PropertyGroup>
      <ItemGroup>
        <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.11" />
        <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="8.0.11" />
        <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.11" />
        <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.8" />
        <PackageVersion Include="Pomelo.EntityFrameworkCore.MySql" Version="8.0.2" />
        <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.11" />
        <PackageVersion Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="8.0.11" />
        <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.11" />
        <PackageVersion Include="Microsoft.Data.Sqlite" Version="8.0.11" />
        <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
        <PackageVersion Include="xunit" Version="2.9.2" />
        <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
      </ItemGroup>
    </Project>
    """;

    private static string ProjectReadme(string name, DatabaseProvider database, bool authenticationEnabled) => authenticationEnabled
        ? AuthenticatedProjectReadme(name, database)
        : PlainProjectReadme(name, database);

    private static string Permissions(string identifier) => $$"""
    namespace {{identifier}}.Api.Authorization;

    public static class Permissions
    {
        public const string ClaimType = "permission";
        public const string ProfileView = "profile.view";
        // DOTISAN:RESOURCE_PERMISSIONS
        public static IReadOnlyList<string> All { get; } = [ProfileView];
    }
    """;

    private static string PlainProjectReadme(string name, DatabaseProvider database) => $$"""
    # {{name}}

    This project was generated by Dotisan. It is a standard ASP.NET Core + Vue/Vite application with EF Core {{DatabaseDisplayName(database)}}.

    ~~~powershell
    dotnet build {{name}}.sln
    dotnet run --project src\{{name}}.Api
    dotnet ef migrations add InitialCreate --project src\{{name}}.Api
    dotnet ef database update --project src\{{name}}.Api
    ~~~

    Create a vertical feature slice:

    ~~~powershell
    dotisan make:resource Customer
    dotisan make:crud Customer
    dotisan generate
    dotisan migrate
    dotisan dev
    ~~~

    Frontend development:

    ~~~powershell
    cd src\{{name}}.Web
    npm run dev
    ~~~

    `dotisan new` installs frontend dependencies before reporting success. If the project was generated with `--no-restore`, run `npm install` or `pnpm install` before `npm run dev`.

    After the wizard, Dotisan checks the .NET SDK, `dotnet-ef`, and the selected frontend package manager. Missing tools are printed with install and verification commands. A missing npm or pnpm skips only frontend installation; the project remains available for native setup commands.

    Press Ctrl+C once while `dotisan dev` is running to stop the API and frontend together. Dotisan allows graceful shutdown before falling back to process-tree cleanup.

    ## Database provider

    {{DatabaseSetup(name, database)}}

    ## Audit foundation

    `Auditing/AuditEntry.cs`, `Auditing/IAuditWriter.cs`, and `Auditing/AuditWriter.cs` are ordinary application source. Audit is enabled by default through `Audit:Enabled`; use the standard `Audit__Enabled=false` override to disable writes. Author the schema with `dotnet ef migrations add InitialAudit --project src\{{name}}.Api` and apply it with `dotnet ef database update --project src\{{name}}.Api`. Generated resource operations record actor, tenant placeholder, action, changed fields, trace ID, and correlation ID. Audit is persistence logging, not event sourcing.

    dotisan.config controls orchestration preferences only. Normal appsettings.json, environment variables, EF Core, and Vite configuration remain the source of truth for their respective concerns. Resource scaffolding creates source files but never creates migrations.
    """;

    private static string AuthenticatedProjectReadme(string name, DatabaseProvider database) => $$"""
    # {{name}}

    This project was generated by Dotisan as a standard ASP.NET Core + Vue/Vite application with EF Core {{DatabaseDisplayName(database)}} and opt-in ASP.NET Core Identity cookie authentication.

    ## Build and run

    ~~~powershell
    dotnet build {{name}}.sln
    dotnet run --project src\{{name}}.Api
    ~~~

    Authentication is enabled because the project was created with `--auth yes`. The generated API exposes:

    - `GET /api/account/antiforgery` for the request token used by state-changing browser calls.
    - `POST /api/account/register` when registration is enabled by the selected policy.
    - `POST /api/account/login` and `POST /api/account/logout`.
    - `GET /api/account/me` for the current authenticated user.

    The generated code uses normal ASP.NET Core Identity, EF Core, cookie authentication, dependency injection, and ProblemDetails. Review and extend these source files directly; Dotisan does not hide the authentication implementation behind a runtime framework.

    ## Database and migrations

    The first Identity migration is authored with standard EF Core tooling:

    ~~~powershell
    dotnet ef migrations add InitialIdentity --project src\{{name}}.Api
    dotnet ef database update --project src\{{name}}.Api
    dotisan migrate
    ~~~

    `dotisan migrate` applies existing migrations. It does not create migrations. Review generated models and migrations before applying them in each environment.

    ## Production configuration

    Use normal ASP.NET Core configuration sources for the connection string and Identity settings. Do not commit production secrets or local SQLite databases. Run behind HTTPS, configure a durable data-protection key ring for multi-instance deployments, and keep cookie, antiforgery, password, lockout, and registration policies appropriate for the deployment environment.

    ## Frontend development

    ~~~powershell
    cd src\{{name}}.Web
    npm run dev
    ~~~

    `dotisan new` installs frontend dependencies before reporting success. If the project was generated with `--no-restore`, run `npm install` or `pnpm install` before `npm run dev`.

    After the wizard, Dotisan checks the .NET SDK, `dotnet-ef`, and the selected frontend package manager. Missing tools are printed with install and verification commands. A missing npm or pnpm skips only frontend installation; the project remains available for native setup commands.

    Press Ctrl+C once while `dotisan dev` is running to stop the API and frontend together. Dotisan allows graceful shutdown before falling back to process-tree cleanup.

    ## Database provider

    {{DatabaseSetup(name, database)}}

    ## Authorization

    `Authorization/Permissions.cs` contains editable permission constants and the generated API registers one standard ASP.NET Core policy per entry. Generated endpoints use explicit `RequireAuthorization(...)` calls. Assign permissions as `permission` claims on standard `IdentityRole` instances with `RoleManager<IdentityRole>`; unauthenticated callers receive `401` and authenticated callers without a required claim receive `403`.

    The generated `/api/authorization/profile` endpoint demonstrates `profile.view`. `dotisan make:resource Customer` adds explicit `customers.view`, `customers.create`, `customers.update`, and `customers.delete` permissions and protects each generated CRUD operation.

    ## Audit foundation

    `Auditing/AuditEntry.cs`, `Auditing/IAuditWriter.cs`, and `Auditing/AuditWriter.cs` are ordinary application source. Audit is enabled by default through `Audit:Enabled`; use the standard `Audit__Enabled=false` override to disable writes. Author the schema with `dotnet ef migrations add InitialAudit --project src\{{name}}.Api` and apply it with `dotnet ef database update --project src\{{name}}.Api`. Generated authentication operations record actor, tenant placeholder, action, trace ID, correlation ID, and redacted changes. Audit is persistence logging, not event sourcing.

    `dotisan.config` controls orchestration preferences only. `appsettings.json`, environment variables, ASP.NET Core services, EF Core configuration, and Vite configuration remain the source of truth for their respective concerns.
    """;

    private static string AuthorizationEndpoints(string identifier) => $$"""
    using {{identifier}}.Api.Authorization;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;

    namespace {{identifier}}.Api.Features.Authorization;

    public static class AuthorizationEndpoints
    {
        public static void MapAuthorizationEndpoints(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/api/authorization/profile", () => Results.Ok(new { permission = Permissions.ProfileView }))
                .RequireAuthorization(Permissions.ProfileView)
                .WithName("AuthorizationProfile")
                .WithTags("Authorization");
        }
    }
    """;

    private static string ApiProgram(string identifier, DatabaseProvider database, bool authenticationEnabled, bool registrationEnabled) => authenticationEnabled
        ? AuthenticatedApiProgram(identifier, database, registrationEnabled)
        : PlainApiProgram(identifier, database);

    private static string PlainApiProgram(string identifier, DatabaseProvider database) => $$"""
    using {{identifier}}.Api.Auditing;
    using Microsoft.EntityFrameworkCore;
    using {{identifier}}.Api.Data;
    using {{identifier}}.Api.Infrastructure;

    if (TryExportDotisanContract(args))
        return;

    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddOpenApi();
    builder.Services.AddProblemDetails();
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");
    builder.Services.AddDbContext<AppDbContext>(options =>
        {{DatabaseRegistration(database)}});
    builder.Services.AddScoped<IAuditWriter, AuditWriter>();

    var app = builder.Build();
    app.UseExceptionHandler();
    app.UseDefaultFiles();
    app.UseStaticFiles();
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }
    app.MapDotisanEndpoints();
    app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
        .WithName("Health")
        .WithTags("System");
    app.MapFallbackToFile("index.html");
    app.Run();

    static bool TryExportDotisanContract(string[] arguments)
    {
        const string option = "--dotisan-export-contract";
        var index = Array.IndexOf(arguments, option);
        if (index < 0)
            return false;
        if (index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index + 1]))
            throw new InvalidOperationException($"{option} requires an output path.");

        var outputPath = Path.GetFullPath(arguments[index + 1]);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, Dotisan.Generated.DotisanContractExport.ContractManifestJson);
        return true;
    }

    public partial class Program { }
    """;

    private static string AuthenticatedApiProgram(string identifier, DatabaseProvider database, bool registrationEnabled) => $$"""
    using System.Security.Claims;
    using {{identifier}}.Api.Authorization;
    using {{identifier}}.Api.Auditing;
    using {{identifier}}.Api.Data;
    using {{identifier}}.Api.Features.Account;
    using {{identifier}}.Api.Features.Authorization;
    using {{identifier}}.Api.Identity;
    using {{identifier}}.Api.Infrastructure;
    using Microsoft.AspNetCore.Authentication.Cookies;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;

    if (TryExportDotisanContract(args))
        return;

    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddOpenApi();
    builder.Services.AddProblemDetails();
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");
    builder.Services.AddDbContext<AppDbContext>(options =>
        {{DatabaseRegistration(database)}});
    builder.Services.AddScoped<IAuditWriter, AuditWriter>();
    builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddRoles<IdentityRole>()
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
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
    builder.Services.AddAuthorization(options =>
    {
        foreach (var permission in Permissions.All)
        {
            options.AddPolicy(permission, policy =>
                policy.RequireClaim(Permissions.ClaimType, permission));
        }
    });
    builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");

    var app = builder.Build();
    app.UseExceptionHandler();
    app.UseDefaultFiles();
    app.UseStaticFiles();
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();
    app.MapDotisanEndpoints();
    app.MapAccountEndpoints({{registrationEnabled.ToString().ToLowerInvariant()}});
    app.MapAuthorizationEndpoints();
    app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
        .WithName("Health")
        .WithTags("System");
    app.MapFallbackToFile("index.html");
    app.Run();

    static bool TryExportDotisanContract(string[] arguments)
    {
        const string option = "--dotisan-export-contract";
        var index = Array.IndexOf(arguments, option);
        if (index < 0)
            return false;
        if (index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index + 1]))
            throw new InvalidOperationException($"{option} requires an output path.");

        var outputPath = Path.GetFullPath(arguments[index + 1]);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, Dotisan.Generated.DotisanContractExport.ContractManifestJson);
        return true;
    }

    public partial class Program { }
    """;

    private static string DatabasePackage(DatabaseProvider database) => database switch
    {
        DatabaseProvider.SqlServer => "Microsoft.EntityFrameworkCore.SqlServer",
        DatabaseProvider.PostgreSQL => "Npgsql.EntityFrameworkCore.PostgreSQL",
        DatabaseProvider.MySQL => "Pomelo.EntityFrameworkCore.MySql",
        _ => "Microsoft.EntityFrameworkCore.Sqlite"
    };

    private static string DatabaseDisplayName(DatabaseProvider database) => database switch
    {
        DatabaseProvider.SqlServer => "SQL Server",
        DatabaseProvider.PostgreSQL => "PostgreSQL",
        DatabaseProvider.MySQL => "MySQL",
        _ => "SQLite defaults"
    };

    private static string DatabaseSetup(string name, DatabaseProvider database) => database switch
    {
        DatabaseProvider.SqlServer => $"This project uses EF Core SQL Server. The local default is `Server=localhost,1433;Database={name};User Id=sa;Password=DotisanDev123!;TrustServerCertificate=True`. The generated `compose.yaml` starts SQL Server with `dotisan dev` or `docker compose up -d --wait --wait-timeout 120 database`; replace the connection string and credentials through standard ASP.NET Core configuration before running migrations. `dotisan dev` stops the container on exit and preserves its named volume.",
        DatabaseProvider.PostgreSQL => $"This project uses EF Core PostgreSQL. The local default is `Host=localhost;Database={name.ToLowerInvariant()};Username=postgres;Password=postgres`. The generated `compose.yaml` starts PostgreSQL with `dotisan dev` or `docker compose up -d --wait --wait-timeout 120 database`; replace the connection string and credentials through standard ASP.NET Core configuration before running migrations. `dotisan dev` stops the container on exit and preserves its named volume.",
        DatabaseProvider.MySQL => $"This project uses EF Core MySQL. The local default is `Server=localhost;Database={name.ToLowerInvariant()};User=root;Password=root`. The generated `compose.yaml` starts MySQL with `dotisan dev` or `docker compose up -d --wait --wait-timeout 120 database`; replace the connection string and credentials through standard ASP.NET Core configuration before running migrations. `dotisan dev` stops the container on exit and preserves its named volume.",
        _ => "This project uses EF Core SQLite. SQLite is file-based and needs no separate database service; the default connection string is `Data Source=app.db`.",
    };

    private static string DatabaseRegistration(DatabaseProvider database) => database switch
    {
        DatabaseProvider.SqlServer => "options.UseSqlServer(connectionString)",
        DatabaseProvider.PostgreSQL => "options.UseNpgsql(connectionString)",
        DatabaseProvider.MySQL => "options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))",
        _ => "options.UseSqlite(connectionString)"
    };

    private static string AppSettings(string name, DatabaseProvider database)
    {
        var connectionString = database switch
        {
            DatabaseProvider.SqlServer => $"Server=localhost,1433;Database={name};User Id=sa;Password=DotisanDev123!;TrustServerCertificate=True",
            DatabaseProvider.PostgreSQL => $"Host=localhost;Database={name.ToLowerInvariant()};Username=postgres;Password=postgres",
            DatabaseProvider.MySQL => $"Server=localhost;Database={name.ToLowerInvariant()};User=root;Password=root",
            _ => "Data Source=app.db"
        };

        return $$"""
        {
          "ConnectionStrings": {
            "DefaultConnection": "{{connectionString}}"
          },
          "Audit": {
            "Enabled": true
          },
          "Logging": {
            "LogLevel": {
              "Default": "Information",
              "Microsoft.AspNetCore": "Warning"
            }
          },
          "AllowedHosts": "*"
        }
        """;
    }

    private static string DatabaseCompose(string name, DatabaseProvider database)
    {
        var databaseName = name.ToLowerInvariant();
        var volumeName = $"{databaseName}-database-data";

        return database switch
        {
            DatabaseProvider.SqlServer => $$"""
            services:
              database:
                image: mcr.microsoft.com/mssql/server:2022-latest
                environment:
                  ACCEPT_EULA: "Y"
                  MSSQL_SA_PASSWORD: "DotisanDev123!"
                ports:
                  - "1433:1433"
                volumes:
                  - {{volumeName}}:/var/opt/mssql
                healthcheck:
                  test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -C -U sa -P 'DotisanDev123!' -Q 'SELECT 1' || exit 1"]
                  interval: 5s
                  timeout: 5s
                  retries: 20
            volumes:
              {{volumeName}}:
            """,
            DatabaseProvider.PostgreSQL => $$"""
            services:
              database:
                image: postgres:16-alpine
                environment:
                  POSTGRES_DB: {{databaseName}}
                  POSTGRES_USER: postgres
                  POSTGRES_PASSWORD: postgres
                ports:
                  - "5432:5432"
                volumes:
                  - {{volumeName}}:/var/lib/postgresql/data
                healthcheck:
                  test: ["CMD-SHELL", "pg_isready -U postgres -d {{databaseName}}"]
                  interval: 5s
                  timeout: 5s
                  retries: 20
            volumes:
              {{volumeName}}:
            """,
            DatabaseProvider.MySQL => $$"""
            services:
              database:
                image: mysql:8.4
                environment:
                  MYSQL_ROOT_PASSWORD: root
                  MYSQL_DATABASE: {{databaseName}}
                ports:
                  - "3306:3306"
                volumes:
                  - {{volumeName}}:/var/lib/mysql
                healthcheck:
                  test: ["CMD-SHELL", "mysqladmin ping -h 127.0.0.1 -uroot -proot --silent"]
                  interval: 5s
                  timeout: 5s
                  retries: 20
            volumes:
              {{volumeName}}:
            """,
            _ => throw new ArgumentOutOfRangeException(nameof(database), database, "SQLite does not have an external database compose service.")
        };
    }

    private static string DbContext(string identifier, bool authenticationEnabled) => authenticationEnabled
        ? IdentityDbContext(identifier)
        : PlainDbContext(identifier);

    private static string PlainDbContext(string identifier) => $$"""
    using {{identifier}}.Api.Auditing;
    using Microsoft.EntityFrameworkCore;

    namespace {{identifier}}.Api.Data;

    public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    }
    """;

    private static string IdentityDbContext(string identifier) => $$"""
    using {{identifier}}.Api.Auditing;
    using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore;
    using {{identifier}}.Api.Identity;

    namespace {{identifier}}.Api.Data;

    public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    }
    """;

    private static string AuditEntry(string identifier) => $$"""
    namespace {{identifier}}.Api.Auditing;

    public sealed class AuditEntry
    {
        public Guid Id { get; set; }
        public string? ActorId { get; set; }
        public string? TenantId { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Changes { get; set; } = "{}";
        public string? TraceId { get; set; }
        public string? CorrelationId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
    """;

    private static string AuditWriterContract(string identifier) => $$"""
    using Microsoft.AspNetCore.Http;

    namespace {{identifier}}.Api.Auditing;

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
    """;

    private static string AuditWriter(string identifier) => $$"""
    using System.Diagnostics;
    using System.Security.Claims;
    using System.Text.Json;
    using {{identifier}}.Api.Data;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;

    namespace {{identifier}}.Api.Auditing;

    public sealed class AuditWriter(AppDbContext db, IConfiguration configuration) : IAuditWriter
    {
        public async Task RecordAsync(
            HttpContext httpContext,
            string entityType,
            string? entityId,
            string action,
            IReadOnlyDictionary<string, object?> changes,
            CancellationToken cancellationToken)
        {
            if (!configuration.GetValue("Audit:Enabled", true))
            {
                return;
            }

            var correlationId = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                ?? httpContext.TraceIdentifier;
            db.AuditEntries.Add(new AuditEntry
            {
                Id = Guid.NewGuid(),
                ActorId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier),
                TenantId = null,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                Changes = JsonSerializer.Serialize(changes),
                TraceId = Activity.Current?.TraceId.ToString(),
                CorrelationId = correlationId,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }
    """;

    private static string ApplicationUser(string identifier) => $$"""
    using Microsoft.AspNetCore.Identity;

    namespace {{identifier}}.Api.Identity;

    public sealed class ApplicationUser : IdentityUser
    {
    }
    """;

    private static string AccountEndpoints(string identifier, bool registrationEnabled) => $$"""
    using System.Security.Claims;
    using {{identifier}}.Api.Auditing;
    using {{identifier}}.Api.Identity;
    using Microsoft.AspNetCore.Authentication.Cookies;
    using Microsoft.AspNetCore.Antiforgery;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Routing;

    namespace {{identifier}}.Api.Features.Account;

    public static class AccountEndpoints
    {
        public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints, bool registrationEnabled)
        {
            var group = endpoints.MapGroup("/api/account");
            group.MapGet("/antiforgery", IssueAntiforgery).AllowAnonymous();
            group.MapPost("/register", (RegisterRequest request, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signInManager, HttpContext httpContext, IAuditWriter audit, CancellationToken cancellationToken) => Register(request, users, signInManager, audit, httpContext, registrationEnabled, cancellationToken)).AllowAnonymous().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapPost("/login", (LoginRequest request, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signInManager, HttpContext httpContext, IAuditWriter audit, CancellationToken cancellationToken) => Login(request, users, signInManager, audit, httpContext, cancellationToken)).AllowAnonymous().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapPost("/logout", (SignInManager<ApplicationUser> signInManager, HttpContext httpContext, IAuditWriter audit, CancellationToken cancellationToken) => Logout(signInManager, audit, httpContext, cancellationToken)).RequireAuthorization().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapGet("/me", (ClaimsPrincipal user) => Me(user)).RequireAuthorization();
            return endpoints;
        }

        private static IResult IssueAntiforgery(HttpContext httpContext, IAntiforgery antiforgery)
        {
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return Results.Ok(new { token = tokens.RequestToken });
        }

        private static async Task<IResult> Register(RegisterRequest request, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signInManager, IAuditWriter audit, HttpContext httpContext, bool registrationEnabled, CancellationToken cancellationToken)
        {
            if (!registrationEnabled)
            {
                await audit.RecordAsync(httpContext, "Security", null, "security.registration.denied", new Dictionary<string, object?>(), cancellationToken);
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Registration is unavailable.", extensions: new Dictionary<string, object?> { ["code"] = "registration_unavailable" });
            }

            var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                var errors = result.Errors
                    .GroupBy(error => error.Code.Contains("Password", StringComparison.OrdinalIgnoreCase) ? "Password" : "Email", StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray(), StringComparer.Ordinal);
                return Results.ValidationProblem(errors);
            }

            signInManager.AuthenticationScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            await signInManager.SignInAsync(user, isPersistent: false);
            await audit.RecordAsync(httpContext, "Security", user.Id, "security.registered", new Dictionary<string, object?>(), cancellationToken);
            return Results.Ok(new CurrentUserResponse(user.Id, user.Email!));
        }

        private static async Task<IResult> Login(LoginRequest request, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signInManager, IAuditWriter audit, HttpContext httpContext, CancellationToken cancellationToken)
        {
            var user = await users.FindByEmailAsync(request.Email);
            if (user is null)
            {
                await audit.RecordAsync(httpContext, "Security", null, "security.login.failed", new Dictionary<string, object?>(), cancellationToken);
                return Results.Unauthorized();
            }

            signInManager.AuthenticationScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            var result = await signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                await audit.RecordAsync(httpContext, "Security", user.Id, "security.login.failed", new Dictionary<string, object?>(), cancellationToken);
                return Results.Unauthorized();
            }

            await audit.RecordAsync(httpContext, "Security", user.Id, "security.login.succeeded", new Dictionary<string, object?>(), cancellationToken);
            return Results.Ok(new CurrentUserResponse(user.Id, user.Email!));
        }

        private static async Task<IResult> Logout(SignInManager<ApplicationUser> signInManager, IAuditWriter audit, HttpContext httpContext, CancellationToken cancellationToken)
        {
            signInManager.AuthenticationScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            await audit.RecordAsync(httpContext, "Security", null, "security.logout", new Dictionary<string, object?>(), cancellationToken);
            await signInManager.SignOutAsync();
            return Results.NoContent();
        }

        private static IResult Me(ClaimsPrincipal user)
        {
            var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = user.FindFirstValue(ClaimTypes.Email);
            return id is null || email is null
                ? Results.Unauthorized()
                : Results.Ok(new CurrentUserResponse(id, email));
        }

        public sealed record RegisterRequest(string Email, string Password);
        public sealed record LoginRequest(string Email, string Password, bool RememberMe);
        public sealed record CurrentUserResponse(string Id, string Email);
    }
    """;

    private static string EndpointExtensions(string identifier) => $$"""
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Routing;

    namespace {{identifier}}.Api.Infrastructure;

    public static class DotisanEndpointExtensions
    {
        // Endpoint registration is intentionally explicit and inspectable.
        public static IEndpointRouteBuilder MapDotisanEndpoints(this IEndpointRouteBuilder endpoints)
        {
            // DOTISAN:ENDPOINTS
            return endpoints;
        }
    }
    """;

    private static string ApiTestsProject(string name, bool authenticationEnabled) => $$"""
    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <NoWarn>CA1707;CS1591</NoWarn>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="Microsoft.NET.Test.Sdk" />
        <PackageReference Include="xunit" />
        <PackageReference Include="xunit.runner.visualstudio" />
        {{(authenticationEnabled ? "<PackageReference Include=\"Microsoft.AspNetCore.Mvc.Testing\" />\n        <PackageReference Include=\"Microsoft.EntityFrameworkCore.Sqlite\" />\n        <PackageReference Include=\"Microsoft.Data.Sqlite\" />" : string.Empty)}}
        <ProjectReference Include="..\\..\\src\\{{name}}.Api\\{{name}}.Api.csproj" />
      </ItemGroup>
    </Project>
    """;

    private static string ApiTests(string identifier) => $$"""
    namespace {{identifier}}.Api.Tests;

    public sealed class HealthEndpointTests
    {
        [Fact]
        public void Generated_test_project_is_ready_for_endpoint_tests()
        {
            Assert.True(true);
        }
    }
    """;

    private static string AuthenticationTests(string identifier, RegistrationPolicy registrationPolicy) => $$"""
    using System.Security.Claims;
    using System.Net;
    using System.Net.Http.Json;
    using {{identifier}}.Api.Authorization;
    using {{identifier}}.Api.Auditing;
    using {{identifier}}.Api.Data;
    using {{identifier}}.Api.Identity;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Data.Sqlite;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;
    using Microsoft.AspNetCore.Identity;

    namespace {{identifier}}.Api.Tests;

    public sealed class AuthenticationEndpointTests(AuthenticationApplicationFactory factory) : IClassFixture<AuthenticationApplicationFactory>
    {
        [Fact]
        public async Task Me_requires_authentication()
        {
            using var client = factory.CreateClient();
            var response = await client.GetAsync("/api/account/me");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Profile_requires_authentication_and_permission()
        {
            using var anonymousClient = factory.CreateClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymousClient.GetAsync("/api/authorization/profile")).StatusCode);

            using var client = await SignInAsync($"no-permission-{Guid.NewGuid():N}@example.com");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/authorization/profile")).StatusCode);
        }

        [Fact]
        public async Task Role_permission_allows_profile()
        {
            using var client = await SignInAsync($"permission-{Guid.NewGuid():N}@example.com", grantProfilePermission: true);

            var response = await client.GetAsync("/api/authorization/profile");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(Permissions.ProfileView, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task Invalid_login_does_not_reveal_account_existence()
        {
            using var client = factory.CreateClient();
            var antiforgery = await GetAntiforgeryToken(client);
            using var login = new HttpRequestMessage(HttpMethod.Post, "/api/account/login");
            login.Headers.Add("X-XSRF-TOKEN", antiforgery);
            login.Content = JsonContent.Create(new { email = "missing@example.com", password = "wrong", rememberMe = false });

            var response = await client.SendAsync(login);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Successful_login_writes_actor_trace_and_correlation()
        {
            var email = $"audit-login-{Guid.NewGuid():N}@example.com";
            await factory.SeedUserAsync(email, grantProfilePermission: false);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
            var antiforgery = await GetAntiforgeryToken(client);
            using var login = new HttpRequestMessage(HttpMethod.Post, "/api/account/login");
            login.Headers.Add("X-XSRF-TOKEN", antiforgery);
            login.Headers.Add("X-Correlation-ID", "correlation-test");
            login.Content = JsonContent.Create(new { email, password = "Password1!", rememberMe = false });

            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(login)).StatusCode);
            var entry = (await factory.ReadAuditEntriesAsync()).Single(item => item.Action == "security.login.succeeded" && item.CorrelationId == "correlation-test");
            Assert.Equal("Security", entry.EntityType);
            Assert.False(string.IsNullOrWhiteSpace(entry.ActorId));
            Assert.False(string.IsNullOrWhiteSpace(entry.TraceId));
        }

        [Fact]
        public async Task Failed_login_is_audited_without_credentials()
        {
            var correlationId = $"failed-{Guid.NewGuid():N}";
            using var client = factory.CreateClient();
            var antiforgery = await GetAntiforgeryToken(client);
            using var login = new HttpRequestMessage(HttpMethod.Post, "/api/account/login");
            login.Headers.Add("X-XSRF-TOKEN", antiforgery);
            login.Headers.Add("X-Correlation-ID", correlationId);
            login.Content = JsonContent.Create(new { email = "missing-audit@example.com", password = "do-not-store-this", rememberMe = false });

            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(login)).StatusCode);
            var entry = (await factory.ReadAuditEntriesAsync()).Single(item => item.Action == "security.login.failed" && item.CorrelationId == correlationId);
            Assert.DoesNotContain("do-not-store-this", entry.Changes, StringComparison.Ordinal);
            Assert.DoesNotContain("missing-audit@example.com", entry.Changes, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Disabled_audit_configuration_skips_writes()
        {
            using var disabledFactory = new AuthenticationApplicationFactory { AuditEnabled = false };
            using var client = disabledFactory.CreateClient();
            var antiforgery = await GetAntiforgeryToken(client);
            using var login = new HttpRequestMessage(HttpMethod.Post, "/api/account/login");
            login.Headers.Add("X-XSRF-TOKEN", antiforgery);
            login.Content = JsonContent.Create(new { email = "disabled-audit@example.com", password = "do-not-store-this", rememberMe = false });

            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(login)).StatusCode);
            Assert.Empty(await disabledFactory.ReadAuditEntriesAsync());
        }

        {{(registrationPolicy == RegistrationPolicy.Public ? PublicAuthenticationTests() : RestrictedRegistrationTest())}}

        private static async Task<string> GetAntiforgeryToken(HttpClient client)
        {
            var response = await client.GetFromJsonAsync<AntiforgeryResponse>("/api/account/antiforgery");
            return response?.Token ?? throw new InvalidOperationException("The generated antiforgery endpoint returned no token.");
        }

        private async Task<HttpClient> SignInAsync(string email, bool grantProfilePermission = false)
        {
            await factory.SeedUserAsync(email, grantProfilePermission);
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
            var antiforgery = await GetAntiforgeryToken(client);
            using var login = new HttpRequestMessage(HttpMethod.Post, "/api/account/login");
            login.Headers.Add("X-XSRF-TOKEN", antiforgery);
            login.Content = JsonContent.Create(new { email, password = "Password1!", rememberMe = false });
            var response = await client.SendAsync(login);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return client;
        }

        private sealed record AntiforgeryResponse(string Token);
    }

    public sealed class AuthenticationApplicationFactory : WebApplicationFactory<Program>
    {
        private SqliteConnection? connection;
        public bool AuditEnabled { get; set; } = true;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Audit:Enabled", AuditEnabled.ToString());
            builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
            builder.ConfigureServices(services =>
            {
                connection = new SqliteConnection("Data Source=:memory:");
                connection.Open();
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));

                using var provider = services.BuildServiceProvider();
                using var scope = provider.CreateScope();
                scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
            });
        }

        public async Task<IReadOnlyList<AuditEntry>> ReadAuditEntriesAsync()
        {
            using var scope = Services.CreateScope();
            var entries = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditEntries
                .AsNoTracking()
                .ToListAsync();
            return entries.OrderBy(entry => entry.CreatedAt).ToArray();
        }

        public async Task SeedUserAsync(string email, bool grantProfilePermission)
        {
            using var scope = Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var user = new ApplicationUser { UserName = email, Email = email };
            var result = await users.CreateAsync(user, "Password1!");
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
            }

            if (!grantProfilePermission)
            {
                return;
            }

            var role = new IdentityRole($"AuthorizationTesters-{Guid.NewGuid():N}");
            var roleResult = await roles.CreateAsync(role);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", roleResult.Errors.Select(error => error.Description)));
            }

            var claimResult = await roles.AddClaimAsync(role, new Claim(Permissions.ClaimType, Permissions.ProfileView));
            if (!claimResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", claimResult.Errors.Select(error => error.Description)));
            }

            var membershipResult = await users.AddToRoleAsync(user, role.Name!);
            if (!membershipResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", membershipResult.Errors.Select(error => error.Description)));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                connection?.Dispose();
            base.Dispose(disposing);
        }
    }
    """;

    private static string PublicAuthenticationTests() => """
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
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/account/me")).StatusCode);
        }

        [Fact]
        public async Task Logout_clears_the_authentication_cookie()
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
            var antiforgery = await GetAntiforgeryToken(client);
            using var register = new HttpRequestMessage(HttpMethod.Post, "/api/account/register");
            register.Headers.Add("X-XSRF-TOKEN", antiforgery);
            register.Content = JsonContent.Create(new { email = "logout@example.com", password = "Password1!" });
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(register)).StatusCode);

            antiforgery = await GetAntiforgeryToken(client);
            using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/account/logout");
            logout.Headers.Add("X-XSRF-TOKEN", antiforgery);
            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(logout)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);
        }
    """;

    private static string RestrictedRegistrationTest() => """
        [Fact]
        public async Task Restricted_registration_returns_a_stable_problem_code()
        {
            using var client = factory.CreateClient();
            var antiforgery = await GetAntiforgeryToken(client);
            using var register = new HttpRequestMessage(HttpMethod.Post, "/api/account/register");
            register.Headers.Add("X-XSRF-TOKEN", antiforgery);
            register.Content = JsonContent.Create(new { email = "person@example.com", password = "Password1!" });

            var response = await client.SendAsync(register);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("registration_unavailable", body, StringComparison.Ordinal);
        }
    """;

    private static string WebIndex(string name) => $$"""
    <!doctype html>
    <html lang="en">
      <head><meta charset="UTF-8" /><meta name="viewport" content="width=device-width, initial-scale=1.0" /><title>{{name}}</title></head>
      <body><div id="app"></div><script type="module" src="/src/main.ts"></script></body>
    </html>
    """;

    private static string ViteConfig() => """
    import { fileURLToPath, URL } from 'node:url';
    import { defineConfig } from 'vite';
    import vue from '@vitejs/plugin-vue';

    export default defineConfig({
      plugins: [vue()],
      resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
      server: { port: 5173, proxy: { '/api': 'https://localhost:5001' } }
    });
    """;

    private static string MainTs() => """
    import { createApp } from 'vue';
    import { createPinia } from 'pinia';
    import { VueQueryPlugin, QueryClient } from '@tanstack/vue-query';
    import router from './routes';
    import App from './App.vue';
    import './style.css';

    const queryClient = new QueryClient();
    createApp(App).use(createPinia()).use(VueQueryPlugin, { queryClient }).use(router).mount('#app');
    """;

    internal static ContractManifest InitialContractManifest(bool authenticationEnabled) => authenticationEnabled
        ? new ContractManifest(
            1,
            [
                new("account.antiforgery", "Account", "IssueAntiforgery", "GET", "/api/account/antiforgery", "System.Void", "AntiforgeryResponse", false, null, null, ["Account"], false, false),
                new("account.login", "Account", "Login", "POST", "/api/account/login", "LoginRequest", "System.Void", false, null, null, ["Account"], true, false),
                new("account.logout", "Account", "Logout", "POST", "/api/account/logout", "System.Void", "System.Void", true, "authenticated", null, ["Account"], false, false),
                new("account.me", "Account", "Me", "GET", "/api/account/me", "System.Void", "CurrentUserResponse", true, "authenticated", null, ["Account"], false, false),
                new("account.register", "Account", "Register", "POST", "/api/account/register", "RegisterRequest", "System.Void", false, null, null, ["Account"], true, false)
            ],
            [
                new("AntiforgeryResponse", "AntiforgeryResponse", [new ContractProperty("token", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], []),
                new("CurrentUserResponse", "CurrentUserResponse", [new ContractProperty("id", new ContractTypeDescriptor(ContractTypeKind.String), false, false), new ContractProperty("email", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], []),
                new("LoginRequest", "LoginRequest", [new ContractProperty("email", new ContractTypeDescriptor(ContractTypeKind.String), false, false), new ContractProperty("password", new ContractTypeDescriptor(ContractTypeKind.String), false, false), new ContractProperty("rememberMe", new ContractTypeDescriptor(ContractTypeKind.Boolean), false, false)], []),
                new("RegisterRequest", "RegisterRequest", [new ContractProperty("email", new ContractTypeDescriptor(ContractTypeKind.String), false, false), new ContractProperty("password", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], [])
            ],
            [
                EndpointContractMetadata.Create("GET", "/api/account/antiforgery", null, [], ["Account"], false),
                EndpointContractMetadata.Create("POST", "/api/account/login", new EndpointRequestBodyMetadata(new ContractTypeDescriptor(ContractTypeKind.Object, "LoginRequest")), [], ["Account"], true),
                EndpointContractMetadata.Create("POST", "/api/account/logout", null, [], ["Account"], false),
                EndpointContractMetadata.Create("GET", "/api/account/me", null, [], ["Account"], false),
                EndpointContractMetadata.Create("POST", "/api/account/register", new EndpointRequestBodyMetadata(new ContractTypeDescriptor(ContractTypeKind.Object, "RegisterRequest")), [], ["Account"], true)
            ])
        : new ContractManifest(1, [], []);

    private static string ContractExport(bool authenticationEnabled) => (authenticationEnabled ? InitialContractManifest(true) : InitialContractManifest(false)).ToJson() switch
    {
        var json => $$"""
    namespace Dotisan.Generated;

    #if DOTISAN_CONTRACT_FALLBACK
    public static class DotisanContractExport
    {
        public const string ContractManifestJson = "{{json.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}}";
    }
    #endif
    """
    };

    private static string RoutesIndex() => """
    import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router';

    const routes: RouteRecordRaw[] = [];
    // DOTISAN:ROUTES

    export default createRouter({
      history: createWebHistory(),
      routes
    });
    """;

    private static string AppVue(string name) => """
    <script setup lang="ts">
    const apiStatus = 'ready';
    </script>

    <template>
      <main class="shell">
        <p class="eyebrow">Dotisan foundation</p>
        <h1>__PROJECT_NAME__</h1>
        <p>Your ASP.NET Core API and Vue frontend are ready for feature work.</p>
        <span class="status">API scaffold {{apiStatus}}</span>
        <RouterView />
      </main>
    </template>
    """.Replace("__PROJECT_NAME__", name, StringComparison.Ordinal);

    private static string StyleCss() => """
    :root { font-family: Inter, ui-sans-serif, system-ui, sans-serif; color: #172033; background: #f6f8fb; }
    body { margin: 0; }
    .shell { max-width: 720px; margin: 12vh auto; padding: 3rem; background: white; border: 1px solid #e2e8f0; border-radius: 1.25rem; box-shadow: 0 1rem 3rem #17203312; }
    .eyebrow { color: #2563eb; font-size: .8rem; font-weight: 700; letter-spacing: .12em; text-transform: uppercase; }
    h1 { margin: .5rem 0; font-size: clamp(2.25rem, 6vw, 4rem); }
    p { color: #526071; line-height: 1.6; }
    .status { display: inline-block; margin-top: 1.25rem; padding: .5rem .75rem; color: #166534; background: #dcfce7; border-radius: 999px; font-size: .9rem; font-weight: 600; }
    """;

    private static string Dockerfile(string name) => $$"""
    FROM node:22-alpine AS web-build
    WORKDIR /src
    COPY src/{{name}}.Web/package*.json ./
    RUN npm install
    COPY src/{{name}}.Web/ ./
    RUN npm run build

    FROM mcr.microsoft.com/dotnet/sdk:8.0 AS api-build
    WORKDIR /src
    COPY src/{{name}}.Api/{{name}}.Api.csproj src/{{name}}.Api/
    RUN dotnet restore src/{{name}}.Api/{{name}}.Api.csproj
    COPY . .
    COPY --from=web-build /src/dist src/{{name}}.Api/wwwroot
    RUN dotnet publish src/{{name}}.Api/{{name}}.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

    FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
    WORKDIR /app
    COPY --from=api-build /app/publish .
    USER $APP_UID
    EXPOSE 8080
    HEALTHCHECK --interval=30s --timeout=5s CMD wget --spider --no-verbose http://localhost:8080/api/health || exit 1
    ENTRYPOINT ["dotnet", "{{name}}.Api.dll"]
    """;

    private static string Solution(string name) => $$"""
    Microsoft Visual Studio Solution File, Format Version 12.00
    # Visual Studio Version 17
    VisualStudioVersion = 17.0.31903.59
    MinimumVisualStudioVersion = 10.0.40219.1
    Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "{{name}}.Api", "src\\{{name}}.Api\\{{name}}.Api.csproj", "{B0000000-0000-0000-0000-000000000001}"
    EndProject
    Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "{{name}}.Api.Tests", "tests\\{{name}}.Api.Tests\\{{name}}.Api.Tests.csproj", "{B0000000-0000-0000-0000-000000000002}"
    EndProject
    Global
        GlobalSection(SolutionConfigurationPlatforms) = preSolution
            Debug|Any CPU = Debug|Any CPU
            Release|Any CPU = Release|Any CPU
        EndGlobalSection
        GlobalSection(ProjectConfigurationPlatforms) = postSolution
            {B0000000-0000-0000-0000-000000000001}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
            {B0000000-0000-0000-0000-000000000001}.Debug|Any CPU.Build.0 = Debug|Any CPU
            {B0000000-0000-0000-0000-000000000001}.Release|Any CPU.ActiveCfg = Release|Any CPU
            {B0000000-0000-0000-0000-000000000001}.Release|Any CPU.Build.0 = Release|Any CPU
            {B0000000-0000-0000-0000-000000000002}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
            {B0000000-0000-0000-0000-000000000002}.Debug|Any CPU.Build.0 = Debug|Any CPU
            {B0000000-0000-0000-0000-000000000002}.Release|Any CPU.ActiveCfg = Release|Any CPU
            {B0000000-0000-0000-0000-000000000002}.Release|Any CPU.Build.0 = Release|Any CPU
        EndGlobalSection
    EndGlobal
    """;
}
