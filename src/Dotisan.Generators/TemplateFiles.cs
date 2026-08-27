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
          "version": "0.1.0",
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
            new("README.md", ProjectReadme(options.Name, options.AuthenticationEnabled)),
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
            new("Dockerfile", Dockerfile(options.Name)),
            new($"src/{options.Name}.Api/{options.Name}.Api.csproj", ApiProject(options.Name, options.AuthenticationEnabled)),
            new($"src/{options.Name}.Api/Program.cs", ApiProgram(identifier, options.AuthenticationEnabled, options.Registration == RegistrationPolicy.Public)),
            new($"src/{options.Name}.Api/Data/AppDbContext.cs", DbContext(identifier, options.AuthenticationEnabled)),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Identity/ApplicationUser.cs", ApplicationUser(identifier)) }
                : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Features/Account/AccountEndpoints.cs", AccountEndpoints(identifier, options.Registration == RegistrationPolicy.Public)) }
                : Array.Empty<TemplateFile>()),
            new($"src/{options.Name}.Api/Infrastructure/DotisanEndpointExtensions.cs", EndpointExtensions(identifier)),
            new($"src/{options.Name}.Api/appsettings.json", "{\n  \"ConnectionStrings\": {\n    \"DefaultConnection\": \"Data Source=app.db\"\n  },\n  \"Logging\": {\n    \"LogLevel\": {\n      \"Default\": \"Information\",\n      \"Microsoft.AspNetCore\": \"Warning\"\n    }\n  },\n  \"AllowedHosts\": \"*\"\n}\n"),
            new($"src/{options.Name}.Api/appsettings.Development.json", "{\n  \"Logging\": {\n    \"LogLevel\": {\n      \"Default\": \"Information\",\n      \"Microsoft.AspNetCore\": \"Information\"\n    }\n  }\n}\n"),
            new($"src/{options.Name}.Web/package.json", packageJson),
            new($"src/{options.Name}.Web/index.html", WebIndex(options.Name)),
            new($"src/{options.Name}.Web/tsconfig.json", "{\n  \"files\": [],\n  \"references\": [{ \"path\": \"./tsconfig.app.json\" }, { \"path\": \"./tsconfig.node.json\" }]\n}\n"),
            new($"src/{options.Name}.Web/tsconfig.app.json", "{\n  \"extends\": \"@vue/tsconfig/tsconfig.dom.json\",\n  \"include\": [\"src/**/*.ts\", \"src/**/*.tsx\", \"src/**/*.vue\"],\n  \"compilerOptions\": {\n    \"composite\": true,\n    \"tsBuildInfoFile\": \"./node_modules/.tmp/tsconfig.app.tsbuildinfo\",\n    \"strict\": true,\n    \"target\": \"ES2022\",\n    \"lib\": [\"ES2022\", \"DOM\", \"DOM.Iterable\"],\n    \"moduleResolution\": \"Bundler\"\n  }\n}\n"),
            new($"src/{options.Name}.Web/tsconfig.node.json", "{\n  \"compilerOptions\": {\n    \"composite\": true,\n    \"tsBuildInfoFile\": \"./node_modules/.tmp/tsconfig.node.tsbuildinfo\",\n    \"module\": \"ESNext\",\n    \"moduleResolution\": \"Bundler\",\n    \"allowSyntheticDefaultImports\": true,\n    \"target\": \"ES2022\",\n    \"types\": [\"node\"]\n  },\n  \"include\": [\"vite.config.ts\"]\n}\n"),
            new($"src/{options.Name}.Web/vite.config.ts", ViteConfig()),
            new($"src/{options.Name}.Web/src/env.d.ts", "/// <reference types=\"vite/client\" />\n"),
            new($"src/{options.Name}.Web/src/main.ts", MainTs()),
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

    private static string ApiProject(string name, bool authenticationEnabled) => $$"""
    <Project Sdk="Microsoft.NET.Sdk.Web">
      <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <RootNamespace>{{name.Replace('-', '_')}}</RootNamespace>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
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

    private static string ProjectReadme(string name, bool authenticationEnabled) => authenticationEnabled
        ? AuthenticatedProjectReadme(name)
        : PlainProjectReadme(name);

    private static string PlainProjectReadme(string name) => $$"""
    # {{name}}

    This project was generated by Dotisan. It is a standard ASP.NET Core + Vue/Vite application with EF Core SQLite defaults.

    ~~~powershell
    dotnet build {{name}}.sln
    dotnet run --project src\{{name}}.Api
    dotnet ef migrations add InitialCreate --project src\{{name}}.Api
    dotnet ef database update --project src\{{name}}.Api
    ~~~

    Create a vertical feature slice:

    ~~~powershell
    dotisan make:resource Customer
    dotisan migrate
    dotisan dev
    ~~~

    Frontend development:

    ~~~powershell
    cd src\{{name}}.Web
    npm install
    npm run dev
    ~~~

    dotisan.config controls orchestration preferences only. Normal appsettings.json, environment variables, EF Core, and Vite configuration remain the source of truth for their respective concerns. Resource scaffolding creates source files but never creates migrations.
    """;

    private static string AuthenticatedProjectReadme(string name) => $$"""
    # {{name}}

    This project was generated by Dotisan as a standard ASP.NET Core + Vue/Vite application with EF Core SQLite defaults and opt-in ASP.NET Core Identity cookie authentication.

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
    npm install
    npm run dev
    ~~~

    `dotisan.config` controls orchestration preferences only. `appsettings.json`, environment variables, ASP.NET Core services, EF Core configuration, and Vite configuration remain the source of truth for their respective concerns.
    """;

    private static string ApiProgram(string identifier, bool authenticationEnabled, bool registrationEnabled) => authenticationEnabled
        ? AuthenticatedApiProgram(identifier, registrationEnabled)
        : PlainApiProgram(identifier);

    private static string PlainApiProgram(string identifier) => $$"""
    using Microsoft.EntityFrameworkCore;
    using {{identifier}}.Api.Data;
    using {{identifier}}.Api.Infrastructure;

    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddProblemDetails();
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

    var app = builder.Build();
    app.UseExceptionHandler();
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapDotisanEndpoints();
    app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
        .WithName("Health")
        .WithTags("System");
    app.MapFallbackToFile("index.html");
    app.Run();

    public partial class Program { }
    """;

    private static string AuthenticatedApiProgram(string identifier, bool registrationEnabled) => $$"""
    using System.Security.Claims;
    using {{identifier}}.Api.Data;
    using {{identifier}}.Api.Features.Account;
    using {{identifier}}.Api.Identity;
    using {{identifier}}.Api.Infrastructure;
    using Microsoft.AspNetCore.Authentication.Cookies;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;

    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddProblemDetails();
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
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

    var app = builder.Build();
    app.UseExceptionHandler();
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();
    app.MapDotisanEndpoints();
    app.MapAccountEndpoints({{registrationEnabled.ToString().ToLowerInvariant()}});
    app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
        .WithName("Health")
        .WithTags("System");
    app.MapFallbackToFile("index.html");
    app.Run();

    public partial class Program { }
    """;

    private static string DbContext(string identifier, bool authenticationEnabled) => authenticationEnabled
        ? IdentityDbContext(identifier)
        : PlainDbContext(identifier);

    private static string PlainDbContext(string identifier) => $$"""
    using Microsoft.EntityFrameworkCore;

    namespace {{identifier}}.Api.Data;

    public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options);
    """;

    private static string IdentityDbContext(string identifier) => $$"""
    using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore;
    using {{identifier}}.Api.Identity;

    namespace {{identifier}}.Api.Data;

    public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options);
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
            group.MapPost("/register", (RegisterRequest request, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signInManager) => Register(request, users, signInManager, registrationEnabled)).AllowAnonymous().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapPost("/login", (LoginRequest request, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signInManager) => Login(request, users, signInManager)).AllowAnonymous().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapPost("/logout", (SignInManager<ApplicationUser> signInManager) => Logout(signInManager)).RequireAuthorization().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapGet("/me", (ClaimsPrincipal user) => Me(user)).RequireAuthorization();
            return endpoints;
        }

        private static IResult IssueAntiforgery(HttpContext httpContext, IAntiforgery antiforgery)
        {
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return Results.Ok(new { token = tokens.RequestToken });
        }

        private static async Task<IResult> Register(RegisterRequest request, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signInManager, bool registrationEnabled)
        {
            if (!registrationEnabled)
            {
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
            return Results.Ok(new CurrentUserResponse(user.Id, user.Email!));
        }

        private static async Task<IResult> Login(LoginRequest request, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signInManager)
        {
            var user = await users.FindByEmailAsync(request.Email);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            signInManager.AuthenticationScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            var result = await signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);
            return result.Succeeded
                ? Results.Ok(new CurrentUserResponse(user.Id, user.Email!))
                : Results.Unauthorized();
        }

        private static async Task<IResult> Logout(SignInManager<ApplicationUser> signInManager)
        {
            signInManager.AuthenticationScheme = CookieAuthenticationDefaults.AuthenticationScheme;
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
        {{(authenticationEnabled ? "<PackageReference Include=\"Microsoft.AspNetCore.Mvc.Testing\" />\n        <PackageReference Include=\"Microsoft.Data.Sqlite\" />" : string.Empty)}}
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
    using System.Net;
    using System.Net.Http.Json;
    using {{identifier}}.Api.Data;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Data.Sqlite;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;

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

        {{(registrationPolicy == RegistrationPolicy.Public ? PublicAuthenticationTests() : RestrictedRegistrationTest())}}

        private static async Task<string> GetAntiforgeryToken(HttpClient client)
        {
            var response = await client.GetFromJsonAsync<AntiforgeryResponse>("/api/account/antiforgery");
            return response?.Token ?? throw new InvalidOperationException("The generated antiforgery endpoint returned no token.");
        }

        private sealed record AntiforgeryResponse(string Token);
    }

    public sealed class AuthenticationApplicationFactory : WebApplicationFactory<Program>
    {
        private SqliteConnection? connection;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
            builder.ConfigureServices(services =>
            {
                connection = new SqliteConnection("Data Source=:memory:");
                connection.Open();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));

                using var provider = services.BuildServiceProvider();
                using var scope = provider.CreateScope();
                scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
            });
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
    import App from './App.vue';
    import './style.css';

    createApp(App).use(createPinia()).mount('#app');
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
