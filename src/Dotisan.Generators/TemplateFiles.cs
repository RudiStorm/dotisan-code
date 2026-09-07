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
          "version": "0.8.6",
          "packageManager": "{{packageManager}}",
          "type": "module",
          "scripts": {
            "dev": "vite",
            "build": "vue-tsc -b && vite build",
            "test": "vitest run --config vitest.config.ts",
            "test:e2e": "playwright test"
          },
          "dependencies": {
            "@tanstack/vue-query": "^5.59.0",
            "pinia": "^2.3.0",
            "vue": "^3.5.0",
            "vue-router": "^4.4.0",
            "zod": "^3.23.0",
            "class-variance-authority": "^0.7.0",
            "clsx": "^2.1.0",
            "tailwind-merge": "^2.5.0"
          },
          "devDependencies": {
            "@types/node": "^22.0.0",
            "@vitejs/plugin-vue": "^5.2.0",
            "@vue/tsconfig": "^0.7.0",
            "typescript": "~5.6.0",
            "vite": "^6.0.0",
            "vitest": "^2.1.0",
            "vue-tsc": "^2.1.0",
            "@vue/test-utils": "^2.4.0",
            "@playwright/test": "^1.49.0",
            "tailwindcss": "^3.4.0",
            "postcss": "8.4.49",
            "autoprefixer": "^10.4.0",
            "jsdom": "^25.0.0"
          }
        }
        """;

        return
        [
            new(".gitignore", "bin/\nobj/\nwwwroot/\nnode_modules/\ndist/\n*.db\n"),
            new("global.json", "{\n  \"sdk\": {\n    \"version\": \"10.0.400\",\n    \"rollForward\": \"latestFeature\",\n    \"allowPrerelease\": false\n  }\n}\n"),
            new(".node-version", "22\n"),
            new("Directory.Packages.props", PackageVersions()),
            new("README.md", ProjectReadme(options.Name, options.Database, options.AuthenticationEnabled, options.MultiTenancyEnabled)),
            new("dotisan.config", $$"""
            version: 1
            profile: quick
            jobs: {{(options.JobsEnabled ? "wolverine" : "none")}}
            database: {{options.Database.ToString().ToLowerInvariant()}}
            authentication: {{(options.AuthenticationEnabled ? "enabled" : "disabled")}}
            multi_tenancy: {{(options.MultiTenancyEnabled ? "enabled" : "disabled")}}
            api_port: {{DevelopmentApiPort(options.Name)}}
            web_port: {{DevelopmentWebPort(options.Name)}}
            mail_provider: {{options.MailProvider.ToString().ToLowerInvariant()}}
            notifications: {{(options.NotificationsEnabled ? "enabled" : "disabled")}}
            storage: {{(options.StorageEnabled ? "enabled" : "disabled")}}
            caching: {{(options.CachingEnabled ? "enabled" : "disabled")}}
            imports_exports: {{(options.ImportsExportsEnabled ? "enabled" : "disabled")}}
            webhooks: {{(options.WebhooksEnabled ? "enabled" : "disabled")}}
            package_manager: {{options.PackageManager.ToString().ToLowerInvariant()}}
            dev:
              services:
                api: true
                frontend: true
                database: true
                observability: false
                mail: {{(options.MailProvider == MailProvider.Mailpit ? "true" : "false")}}
                workers: false
            """),
            new("dotisan.contract.json", InitialContractManifest(options.AuthenticationEnabled).ToJson()),
            new("Dockerfile", Dockerfile(options.Name, options.PackageManager)),
            new TemplateFile("compose.yaml", DatabaseCompose(options.Name, options.Database, options.MailProvider)),
            new($"src/{options.Name}.Api/{options.Name}.Api.csproj", ApiProject(options.Name, options.Database, options.AuthenticationEnabled, options.JobsEnabled)),
            new($"src/{options.Name}.Api/Program.cs", ApiProgram(identifier, options.Database, options.AuthenticationEnabled, options.Registration == RegistrationPolicy.Public, options.MultiTenancyEnabled, options.NotificationsEnabled, options.StorageEnabled, options.CachingEnabled, options.ImportsExportsEnabled, options.WebhooksEnabled, options.JobsEnabled)),
            new($"src/{options.Name}.Api/Data/AppDbContext.cs", DbContext(identifier, options.AuthenticationEnabled, options.NotificationsEnabled, options.WebhooksEnabled, options.ImportsExportsEnabled)),
            new($"src/{options.Name}.Api/Auditing/AuditEntry.cs", AuditEntry(identifier)),
            new($"src/{options.Name}.Api/Auditing/IAuditWriter.cs", AuditWriterContract(identifier)),
            new($"src/{options.Name}.Api/Auditing/AuditWriter.cs", AuditWriter(identifier, options.MultiTenancyEnabled)),
            ..(options.MultiTenancyEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Tenancy/TenantContext.cs", TenantContext(identifier)) }
                : Array.Empty<TemplateFile>()),
            ..(options.JobsEnabled ? new[] { new TemplateFile($"src/{options.Name}.Api/Jobs/JobRegistration.cs", JobRegistration(identifier, options.Database)), new TemplateFile($"src/{options.Name}.Api/Jobs/SampleJob.cs", SampleJob(identifier)), new TemplateFile($"src/{options.Name}.Api/Jobs/SampleJobHandler.cs", SampleJobHandler(identifier)), new TemplateFile($"src/{options.Name}.Api/Features/Jobs/JobEndpoints.cs", JobEndpoints(identifier, options.AuthenticationEnabled)) } : Array.Empty<TemplateFile>()),
            new($"src/{options.Name}.Api/Features/Health/HealthEndpoints.cs", HealthEndpoints(identifier)),
            new($"src/{options.Name}.Api/Infrastructure/DotisanSecurityOptions.cs", DotisanSecurityOptions(identifier)),
            new($"src/{options.Name}.Api/Infrastructure/DotisanProductionConfiguration.cs", DotisanProductionConfiguration(identifier, options.AuthenticationEnabled)),
            ..(options.NotificationsEnabled ? new[] { new TemplateFile($"src/{options.Name}.Api/Integrations/Notifications.cs", Notifications(identifier, options.AuthenticationEnabled, options.MultiTenancyEnabled)) } : Array.Empty<TemplateFile>()),
            ..(options.StorageEnabled ? new[] { new TemplateFile($"src/{options.Name}.Api/Integrations/Storage.cs", Storage(identifier, options.AuthenticationEnabled, options.MultiTenancyEnabled)) } : Array.Empty<TemplateFile>()),
            ..(options.CachingEnabled ? new[] { new TemplateFile($"src/{options.Name}.Api/Integrations/Caching.cs", Caching(identifier)) } : Array.Empty<TemplateFile>()),
            ..(options.ImportsExportsEnabled ? new[] { new TemplateFile($"src/{options.Name}.Api/Integrations/ImportsExports.cs", ImportsExports(identifier, options.AuthenticationEnabled, options.MultiTenancyEnabled)) } : Array.Empty<TemplateFile>()),
            ..(options.WebhooksEnabled ? new[] { new TemplateFile($"src/{options.Name}.Api/Integrations/Webhooks.cs", Webhooks(identifier, options.AuthenticationEnabled, options.MultiTenancyEnabled)) } : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Identity/ApplicationUser.cs", ApplicationUser(identifier)) }
                : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Identity/ApplicationSession.cs", ApplicationSession(identifier)) }
                : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Integrations/IEmailProvider.cs", EmailSender(identifier)) }
                : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Integrations/IExternalLoginProvider.cs", ExternalLoginProvider(identifier)) }
                : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Integrations/IntegrationExamples.cs", IntegrationExamples(identifier)) }
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
            new($"src/{options.Name}.Api/Infrastructure/DotisanEndpointExtensions.cs", EndpointExtensions(identifier, options.AuthenticationEnabled, options.Registration == RegistrationPolicy.Public, options.NotificationsEnabled, options.StorageEnabled, options.ImportsExportsEnabled, options.WebhooksEnabled, options.JobsEnabled)),
            new($"src/{options.Name}.Api/appsettings.json", AppSettings(options.Name, options.Database, DevelopmentWebPort(options.Name), options.JobsEnabled)),
            new($"src/{options.Name}.Api/appsettings.Development.json", DevelopmentAppSettings(options.MailProvider)),
            new($"src/{options.Name}.Web/package.json", packageJson),
            new($"src/{options.Name}.Web/index.html", WebIndex(options.Name)),
            new($"src/{options.Name}.Web/tsconfig.json", "{\n  \"files\": [],\n  \"references\": [{ \"path\": \"./tsconfig.app.json\" }, { \"path\": \"./tsconfig.node.json\" }]\n}\n"),
            new($"src/{options.Name}.Web/tsconfig.app.json", "{\n  \"extends\": \"@vue/tsconfig/tsconfig.dom.json\",\n  \"include\": [\"src/**/*.ts\", \"src/**/*.tsx\", \"src/**/*.vue\"],\n  \"compilerOptions\": {\n    \"composite\": true,\n    \"tsBuildInfoFile\": \"./node_modules/.tmp/tsconfig.app.tsbuildinfo\",\n    \"strict\": true,\n    \"skipLibCheck\": true,\n    \"target\": \"ES2022\",\n    \"lib\": [\"ES2022\", \"DOM\", \"DOM.Iterable\"],\n    \"moduleResolution\": \"Bundler\"\n  }\n}\n"),
            new($"src/{options.Name}.Web/tsconfig.node.json", "{\n  \"compilerOptions\": {\n    \"composite\": true,\n    \"tsBuildInfoFile\": \"./node_modules/.tmp/tsconfig.node.tsbuildinfo\",\n    \"module\": \"ESNext\",\n    \"moduleResolution\": \"Bundler\",\n    \"allowSyntheticDefaultImports\": true,\n    \"skipLibCheck\": true,\n    \"target\": \"ES2022\",\n    \"types\": [\"node\"]\n  },\n  \"include\": [\"vite.config.ts\"]\n}\n"),
            new($"src/{options.Name}.Web/vite.config.ts", ViteConfig(DevelopmentApiPort(options.Name), DevelopmentWebPort(options.Name))),
            new($"src/{options.Name}.Web/tailwind.config.ts", TailwindConfig()),
            new($"src/{options.Name}.Web/postcss.config.cjs", PostCssConfig()),
            new($"src/{options.Name}.Web/playwright.config.ts", PlaywrightConfig()),
            new($"src/{options.Name}.Web/vitest.config.ts", VitestConfig()),
            new($"src/{options.Name}.Web/components.json", ShadcnComponentsConfig()),
            new($"src/{options.Name}.Web/src/env.d.ts", "/// <reference types=\"vite/client\" />\n"),
            new($"src/{options.Name}.Web/src/main.ts", MainTs()),
            new($"src/{options.Name}.Web/src/routes/index.ts", RoutesIndex(options.AuthenticationEnabled, options.NotificationsEnabled, options.ImportsExportsEnabled, options.WebhooksEnabled)),
            new($"src/{options.Name}.Web/src/App.vue", AppVue()),
            new($"src/{options.Name}.Web/src/style.css", StyleCss()),
            new($"src/{options.Name}.Web/src/dotisan/.gitkeep", string.Empty),
            new($"src/{options.Name}.Web/src/api/client.ts", ApiClient()),
            new($"src/{options.Name}.Web/src/components/ui/Button.vue", UiButton()),
            new($"src/{options.Name}.Web/src/components/ui/Input.vue", UiInput()),
            new($"src/{options.Name}.Web/src/components/ui/Card.vue", UiCard()),
            new($"src/{options.Name}.Web/src/components/ui/Badge.vue", UiBadge()),
            new($"src/{options.Name}.Web/src/lib/utils.ts", ShadcnUtils()),
            new($"src/{options.Name}.Web/src/components/ui/Badge.test.ts", UiBadgeTest()),
            new($"src/{options.Name}.Web/tests/e2e/shell.spec.ts", FrontendSmokeTest()),
            new($"src/{options.Name}.Web/src/components/AppSidebar.vue", AppSidebar(options.Name, options.AuthenticationEnabled)),
            new($"src/{options.Name}.Web/src/components/AppHeader.vue", AppHeader(options.Name, options.AuthenticationEnabled)),
            new($"src/{options.Name}.Web/src/layouts/PortalLayout.vue", PortalLayout()),
            new($"src/{options.Name}.Web/src/layouts/AuthLayout.vue", AuthLayout()),
            new($"src/{options.Name}.Web/src/pages/DashboardPage.vue", DashboardPage(options.Name)),
            ..(options.NotificationsEnabled ? new[] { new TemplateFile($"src/{options.Name}.Web/src/pages/NotificationsPage.vue", NotificationsPage()) } : Array.Empty<TemplateFile>()),
            ..(options.ImportsExportsEnabled ? new[] { new TemplateFile($"src/{options.Name}.Web/src/pages/ImportsExportsPage.vue", ImportsExportsPage()) } : Array.Empty<TemplateFile>()),
            ..(options.WebhooksEnabled ? new[] { new TemplateFile($"src/{options.Name}.Web/src/pages/WebhooksPage.vue", WebhooksPage()) } : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] {
                    new TemplateFile($"src/{options.Name}.Web/src/pages/auth/LoginPage.vue", LoginPage()),
                    new TemplateFile($"src/{options.Name}.Web/src/pages/auth/RegisterPage.vue", RegisterPage()),
                    new TemplateFile($"src/{options.Name}.Web/src/pages/auth/ForgotPasswordPage.vue", ForgotPasswordPage()),
                    new TemplateFile($"src/{options.Name}.Web/src/pages/auth/EmailConfirmationPage.vue", EmailConfirmationPage()),
                    new TemplateFile($"src/{options.Name}.Web/src/pages/auth/MfaChallengePage.vue", MfaChallengePage()),
                    new TemplateFile($"src/{options.Name}.Web/src/pages/account/ProfilePage.vue", ProfilePage()),
                    new TemplateFile($"src/{options.Name}.Web/src/pages/account/MfaPage.vue", MfaPage()),
                    new TemplateFile($"src/{options.Name}.Web/src/pages/account/SessionsPage.vue", SessionsPage()),
                    new TemplateFile($"src/{options.Name}.Web/src/pages/account/ExternalLoginsPage.vue", ExternalLoginsPage()),
                    new TemplateFile($"src/{options.Name}.Web/src/pages/admin/AuthorizationPage.vue", AuthorizationPage())
                }
                : Array.Empty<TemplateFile>()),
            new($"tests/{options.Name}.Api.Tests/{options.Name}.Api.Tests.csproj", ApiTestsProject(options.Name, options.AuthenticationEnabled)),
            new($"tests/{options.Name}.Api.Tests/HealthEndpointTests.cs", ApiTests(identifier)),
            ..(options.JobsEnabled ? new[] { new TemplateFile($"tests/{options.Name}.Api.Tests/JobTests.cs", JobTests(identifier)) } : Array.Empty<TemplateFile>()),
            new($"tests/{options.Name}.Api.Tests/Usings.cs", "global using Xunit;\n"),
            ..(options.MultiTenancyEnabled
                ? new[] { new TemplateFile($"tests/{options.Name}.Api.Tests/TenantContextTests.cs", TenantContextTests(identifier)) }
                : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"tests/{options.Name}.Api.Tests/AuthenticationEndpointTests.cs", AuthenticationTests(identifier, options.Registration)) }
                : Array.Empty<TemplateFile>()),
            new($"{options.Name}.sln", Solution(options.Name))
        ];
    }

    private static string ApiProject(string name, DatabaseProvider database, bool authenticationEnabled, bool jobsEnabled) => $$"""
    <Project Sdk="Microsoft.NET.Sdk.Web">
      <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <RootNamespace>{{name.Replace('-', '_')}}</RootNamespace>
        <OpenApiGenerateDocuments>true</OpenApiGenerateDocuments>
        <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="{{DatabasePackage(database)}}" />
        <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
        <PackageReference Include="Microsoft.Extensions.ApiDescription.Server" PrivateAssets="all" />
        {{(jobsEnabled ? $"<PackageReference Include=\"WolverineFx\" />\n        <PackageReference Include=\"WolverineFx.EntityFrameworkCore\" />\n        <PackageReference Include=\"WolverineFx.RuntimeCompilation\" />\n        <PackageReference Include=\"WolverineFx.{WolverineProviderPackage(database)}\" />" : string.Empty)}}
        <PackageReference Include="OpenTelemetry.Extensions.Hosting" />
        <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
        <PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" />
        <PackageReference Include="OpenTelemetry.Instrumentation.Http" />
        <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />
        <PackageReference Include="Microsoft.EntityFrameworkCore.Design" PrivateAssets="all" />
        {{(authenticationEnabled ? "<PackageReference Include=\"Microsoft.AspNetCore.Identity.EntityFrameworkCore\" />" : string.Empty)}}
      </ItemGroup>
    </Project>
    """;

    private static string PackageVersions() => """
    <Project>
      <PropertyGroup>
        <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
        <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
      </PropertyGroup>
      <ItemGroup>
        <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.7" />
        <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.7" />
        <PackageVersion Include="Microsoft.Extensions.ApiDescription.Server" Version="10.0.7" />
        <PackageVersion Include="Microsoft.OpenApi" Version="2.7.5" />
        <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.7" />
        <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.7" />
        <PackageVersion Include="Pomelo.EntityFrameworkCore.MySql" Version="10.0.7" />
        <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.7" />
        <PackageVersion Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.7" />
        <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.7" />
        <PackageVersion Include="Microsoft.Data.Sqlite" Version="10.0.9" />
        <PackageVersion Include="Microsoft.Extensions.Logging" Version="10.0.7" />
        <PackageVersion Include="WolverineFx" Version="6.30.3" />
        <PackageVersion Include="WolverineFx.EntityFrameworkCore" Version="6.30.3" />
        <PackageVersion Include="WolverineFx.RuntimeCompilation" Version="6.30.3" />
        <PackageVersion Include="WolverineFx.Sqlite" Version="6.30.3" />
        <PackageVersion Include="WolverineFx.SqlServer" Version="6.30.3" />
        <PackageVersion Include="WolverineFx.Postgresql" Version="6.30.3" />
        <PackageVersion Include="WolverineFx.MySql" Version="6.30.3" />
        <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.18.0" />
        <PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.18.0" />
        <PackageVersion Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.18.0-beta.1" />
        <PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="1.18.0" />
        <PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.18.0" />
        <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
        <PackageVersion Include="xunit" Version="2.9.2" />
        <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
      </ItemGroup>
    </Project>
    """;

    private static string ProjectReadme(string name, DatabaseProvider database, bool authenticationEnabled, bool multiTenancyEnabled) => authenticationEnabled
        ? AuthenticatedProjectReadme(name, database, multiTenancyEnabled)
        : PlainProjectReadme(name, database, multiTenancyEnabled);

    private static string WolverineProviderPackage(DatabaseProvider database) => database switch
    {
        DatabaseProvider.SqlServer => "SqlServer",
        DatabaseProvider.PostgreSQL => "Postgresql",
        DatabaseProvider.MySQL => "MySql",
        _ => "Sqlite"
    };

    private static string JobRegistration(string identifier, DatabaseProvider database) => $$"""
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Wolverine;
    using Wolverine.ErrorHandling;
    using Wolverine.{{WolverineProviderPackage(database)}};
    using Wolverine.EntityFrameworkCore;

    namespace {{identifier}}.Api.Jobs;

    public static class JobRegistration
    {
        // DOTISAN:SCHEDULE sample|SampleJob|300|true
        public static void Configure(WolverineOptions options, string connectionString, IConfiguration configuration)
        {
            var jobsEnabled = configuration.GetValue("Dotisan:Jobs:Enabled", true);
            if (jobsEnabled)
            {
                options.{{WolverinePersistenceMethod(database)}}(connectionString);
                options.Policies.UseDurableLocalQueues();
                options.Services.AddHostedService<SampleJobScheduleStarter>();
            }
            options.UseEntityFrameworkCoreTransactions();
            if (jobsEnabled)
            {
                ((IWithFailurePolicies)options.Policies).OnException<Exception>()
                    .ScheduleRetry(TimeSpan.FromSeconds(configuration.GetValue("Dotisan:Jobs:RetryDelaySeconds", 5)));
            }
            SampleJobHandler.ConfigureRetry(
                configuration.GetValue("Dotisan:Jobs:MaxAttempts", 3),
                configuration.GetValue("Dotisan:Jobs:RetryDelaySeconds", 5));
        }
    }

    internal sealed class SampleJobScheduleStarter(IServiceScopeFactory scopeFactory, IConfiguration configuration) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (configuration.GetValue("Dotisan:Jobs:Enabled", true))
            {
                using var scope = scopeFactory.CreateScope();
                var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
                await bus.ScheduleAsync(new SampleJob(DateTimeOffset.UtcNow, true), TimeSpan.FromSeconds(300));
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
    """;

    private static string WolverinePersistenceMethod(DatabaseProvider database) => database switch
    {
        DatabaseProvider.SqlServer => "PersistMessagesWithSqlServer",
        DatabaseProvider.PostgreSQL => "PersistMessagesWithPostgresql",
        DatabaseProvider.MySQL => "PersistMessagesWithMySql",
        _ => "PersistMessagesWithSqlite"
    };

    private static string SampleJob(string identifier) => $$"""
    namespace {{identifier}}.Api.Jobs;

    public sealed record SampleJob(DateTimeOffset EnqueuedAt, bool Recurring = false);
    """;

    private static string SampleJobHandler(string identifier) => $$"""
    using Microsoft.Extensions.Logging;
    using Wolverine;
    using Wolverine.Configuration;
    using Wolverine.Runtime.Handlers;

    namespace {{identifier}}.Api.Jobs;

    public sealed partial class SampleJobHandler : IHandlerConfiguration
    {
        private static int _maxAttempts = 3;
        public static int AttemptCount { get; private set; }
        public static int FailuresBeforeSuccess { get; set; }
        public static int ProcessedCount { get; private set; }

        public static void ResetTestCounters()
        {
            AttemptCount = 0;
            ProcessedCount = 0;
            FailuresBeforeSuccess = 0;
        }

        public static void ConfigureRetry(int maxAttempts, int retryDelaySeconds)
        {
            _ = retryDelaySeconds;
            _maxAttempts = Math.Max(1, maxAttempts);
        }

        public static void Configure(HandlerChain chain)
        {
            chain.Failures.MaximumAttempts = _maxAttempts;
        }

        public static OutgoingMessages Handle(SampleJob message, ILogger<SampleJobHandler> logger)
        {
            AttemptCount++;
            if (FailuresBeforeSuccess > 0)
            {
                FailuresBeforeSuccess--;
                throw new InvalidOperationException("Configured sample-job test failure.");
            }
            ProcessedCount++;
            LogProcessed(logger, message.EnqueuedAt);
            var messages = new OutgoingMessages();
            if (message.Recurring)
                messages.Delay(new SampleJob(DateTimeOffset.UtcNow, true), TimeSpan.FromSeconds(300));
            return messages;
        }

        [LoggerMessage(EventId = 6000, Level = LogLevel.Information, Message = "Processed sample job enqueued at {EnqueuedAt}.")]
        private static partial void LogProcessed(ILogger logger, DateTimeOffset enqueuedAt);
    }
    """;

    private static string JobEndpoints(string identifier, bool authenticationEnabled) => $$"""
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    {{(authenticationEnabled ? "using Microsoft.AspNetCore.Antiforgery;" : string.Empty)}}
    using Wolverine;
    using {{identifier}}.Api.Jobs;
    {{(authenticationEnabled ? $"using {identifier}.Api.Security;" : string.Empty)}}

    namespace {{identifier}}.Api.Features.Jobs;

    public static class JobEndpoints
    {
        public static void MapJobEndpoints(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost("/api/jobs/sample", async (IMessageBus bus, CancellationToken cancellationToken) =>
            {
                await bus.SendAsync(new SampleJob(DateTimeOffset.UtcNow));
                return Results.Accepted();
            }){{(authenticationEnabled ? ".RequireAuthorization(Permissions.AuthorizationManage)" : string.Empty)}}.WithName("EnqueueSampleJob").WithTags("Jobs");

            endpoints.MapPost("/api/jobs/sample/schedule", async (IMessageBus bus, CancellationToken cancellationToken) =>
            {
                await bus.ScheduleAsync(new SampleJob(DateTimeOffset.UtcNow), TimeSpan.FromMinutes(5));
                return Results.Accepted();
            }){{(authenticationEnabled ? ".RequireAuthorization(Permissions.AuthorizationManage)" : string.Empty)}}.WithName("ScheduleSampleJob").WithTags("Jobs");
        }
    }
    """;

    private static string HealthEndpoints(string identifier) => $$"""
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Diagnostics.HealthChecks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using {{identifier}}.Api.Data;

    namespace {{identifier}}.Api.Features.Health;

    public static class HealthEndpoints
    {
        public static void MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
            endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
            endpoints.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
                .WithName("Health")
                .WithTags("System");
        }
    }

    public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("The configured database is unavailable.");
        }
    }
    """;

    private static string DotisanSecurityOptions(string identifier) => $$"""
    using System.ComponentModel.DataAnnotations;

    namespace {{identifier}}.Api.Infrastructure;

    public sealed class DotisanSecurityOptions
    {
        [Url]
        public string? FrontendUrl { get; set; }

        public string? DataProtectionKeyDirectory { get; set; }

        public string[] KnownProxies { get; set; } = [];
    }
    """;

    private static string DotisanProductionConfiguration(string identifier, bool emailConfirmationEnabled) => $$"""
    using Microsoft.Extensions.Configuration;

    namespace {{identifier}}.Api.Infrastructure;

    public static class DotisanProductionConfiguration
    {
        public static void Validate(IConfiguration configuration, string environmentName, bool emailConfirmationEnabled = {{emailConfirmationEnabled.ToString().ToLowerInvariant()}}, bool dataProtectionEnabled = true)
        {
            if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase))
                return;

            var frontendUrl = configuration["Dotisan:Security:FrontendUrl"] ?? configuration["FrontendUrl"];
            if (!Uri.TryCreate(frontendUrl, UriKind.Absolute, out var parsedFrontendUrl) || parsedFrontendUrl.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Dotisan:Security:FrontendUrl must be an absolute HTTPS URL outside Development.");

            if (dataProtectionEnabled)
            {
                var keyDirectory = configuration["Dotisan:Security:DataProtectionKeyDirectory"] ?? configuration["DataProtection:KeyDirectory"];
                if (string.IsNullOrWhiteSpace(keyDirectory))
                    throw new InvalidOperationException("Dotisan:Security:DataProtectionKeyDirectory is required outside Development.");
            }

            if (emailConfirmationEnabled)
            {
                var mailProvider = configuration["Mail:Provider"]?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(mailProvider) || mailProvider is "console" or "mailpit")
                    throw new InvalidOperationException("Mail:Provider must be smtp or a custom provider outside Development.");
            }
        }
    }
    """;

    private static string Permissions(string identifier) => $$"""
    namespace {{identifier}}.Api.Authorization;

    public static class Permissions
    {
        public const string ClaimType = "permission";
        public const string ProfileView = "profile.view";
        public const string AuthorizationManage = "authorization.manage";
        // DOTISAN:RESOURCE_PERMISSIONS
        public static IReadOnlyList<string> All { get; } = [ProfileView, AuthorizationManage];
    }
    """;

    private static string PlainProjectReadme(string name, DatabaseProvider database, bool multiTenancyEnabled) => $$"""
    # {{name}}

    This project was generated by Dotisan. It is a standard ASP.NET Core + Vue/Vite application with EF Core {{DatabaseDisplayName(database)}}.

    ~~~powershell
    dotnet build {{name}}.sln
    dotnet run --project src\{{name}}.Api
    dotnet ef migrations add AddOrders --project src\{{name}}.Api
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

    The Vue portal starts with a componentized dashboard shell, sidebar, header, and local shadcn-vue-style primitives under `src\{{name}}.Web\src\components`. Contract output is kept in `src\{{name}}.Web\src\dotisan` so generated API clients stay separate from application UI code.

    `dotisan new` installs frontend dependencies before reporting success. If the project was generated with `--no-restore`, run `npm install` or `pnpm install` before `npm run dev`.

    After the wizard, Dotisan checks the .NET SDK, `dotnet-ef`, and the selected frontend package manager. Missing tools are printed with install and verification commands. A missing npm or pnpm skips only frontend installation; the project remains available for native setup commands.

    Press Ctrl+C once while `dotisan dev` is running to stop the API and frontend together. Dotisan allows graceful shutdown before falling back to process-tree cleanup.

    During `dotisan dev`, the API listens on `http://localhost:5000` and Vite proxies `/api` requests to that address. When running the processes manually, use `dotnet run --project src\{{name}}.Api --urls http://localhost:5000` so the frontend can reach the API.

    ## Database provider

    {{DatabaseSetup(name, database)}}

    ## Audit foundation

    `Auditing/AuditEntry.cs`, `Auditing/IAuditWriter.cs`, and `Auditing/AuditWriter.cs` are ordinary application source. Audit is enabled by default through `Audit:Enabled`; use the standard `Audit__Enabled=false` override to disable writes. Author the schema with `dotnet ef migrations add InitialAudit --project src\{{name}}.Api` and apply it with `dotnet ef database update --project src\{{name}}.Api`. Generated resource operations record actor, tenant placeholder, action, changed fields, trace ID, and correlation ID. Audit is persistence logging, not event sourcing.

    dotisan.config controls orchestration preferences only. Normal appsettings.json, environment variables, EF Core, and Vite configuration remain the source of truth for their respective concerns. Resource scaffolding creates source files but never creates migrations.
    """;

    private static string AuthenticatedProjectReadme(string name, DatabaseProvider database, bool multiTenancyEnabled) => $$"""
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

    The Vue portal includes ready-to-edit login, registration, forgot-password, and profile pages under `src\{{name}}.Web\src\pages`, composed from local shadcn-vue-style primitives. The authenticated portal shell keeps generated API contracts in `src\{{name}}.Web\src\dotisan` and application UI in `components`, `layouts`, and `pages`.

    ## Database and migrations

    `dotisan new` does not create or apply migrations. Author and apply the initial Identity migration manually with standard EF Core tooling:

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

    During `dotisan dev`, the API listens on `http://localhost:5000` and Vite proxies `/api` requests to that address. When running the processes manually, use `dotnet run --project src\{{name}}.Api --urls http://localhost:5000` so the frontend can reach the API.

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
    using {{identifier}}.Api.Identity;
    using {{identifier}}.Api.Integrations;
    using {{identifier}}.Api.Infrastructure;
    using {{identifier}}.Api.Features.Health;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.AspNetCore.Antiforgery;

    namespace {{identifier}}.Api.Features.Authorization;

    public static class AuthorizationEndpoints
    {
        public static void MapAuthorizationEndpoints(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/api/authorization/profile", () => Results.Ok(new { permission = Permissions.ProfileView }))
                .RequireAuthorization(Permissions.ProfileView)
                .WithName("AuthorizationProfile")
                .WithTags("Authorization");
            endpoints.MapGet("/api/authorization/users", (UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles) =>
                Results.Ok(new { users = users.Users.Select(user => new { id = user.Id, email = user.Email }).ToArray(), roles = roles.Roles.Select(role => role.Name).ToArray() }))
                .RequireAuthorization(Permissions.AuthorizationManage)
                .WithName("AuthorizationUsers").WithTags("Authorization");
            endpoints.MapPost("/api/authorization/users/{userId}/roles/{roleName}", async (string userId, string roleName, UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles) =>
            {
                var user = await users.FindByIdAsync(userId);
                if (user is null || !await roles.RoleExistsAsync(roleName)) return Results.NotFound();
                var result = await users.AddToRoleAsync(user, roleName);
                return result.Succeeded ? Results.NoContent() : Results.ValidationProblem(result.Errors.GroupBy(error => error.Code).ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray()));
            }).RequireAuthorization(Permissions.AuthorizationManage).WithMetadata(new RequireAntiforgeryTokenAttribute(true)).WithName("AuthorizationAssignRole").WithTags("Authorization");
        }
    }
    """;

    private static string ApiProgram(string identifier, DatabaseProvider database, bool authenticationEnabled, bool registrationEnabled, bool multiTenancyEnabled, bool notificationsEnabled, bool storageEnabled, bool cachingEnabled, bool importsExportsEnabled, bool webhooksEnabled, bool jobsEnabled) => authenticationEnabled
        ? AuthenticatedApiProgram(identifier, database, registrationEnabled, multiTenancyEnabled, notificationsEnabled, storageEnabled, cachingEnabled, importsExportsEnabled, webhooksEnabled, jobsEnabled)
        : PlainApiProgram(identifier, database, multiTenancyEnabled, notificationsEnabled, storageEnabled, cachingEnabled, importsExportsEnabled, webhooksEnabled, jobsEnabled);

    private static string PlainApiProgram(string identifier, DatabaseProvider database, bool multiTenancyEnabled, bool notificationsEnabled, bool storageEnabled, bool cachingEnabled, bool importsExportsEnabled, bool webhooksEnabled, bool jobsEnabled) => $$"""
    using {{identifier}}.Api.Auditing;
    {{(multiTenancyEnabled ? $"using {identifier}.Api.Tenancy;" : string.Empty)}}
    using Microsoft.EntityFrameworkCore;
    using {{identifier}}.Api.Data;
    using {{identifier}}.Api.Infrastructure;
    {{(jobsEnabled ? $"using {identifier}.Api.Jobs;" : string.Empty)}}
    using {{identifier}}.Api.Features.Health;
    using Microsoft.AspNetCore.SignalR;
    {{(notificationsEnabled || storageEnabled || cachingEnabled || importsExportsEnabled || webhooksEnabled ? $"using {identifier}.Api.Integrations;" : string.Empty)}}
    using Wolverine;
    using OpenTelemetry;
    using OpenTelemetry.Metrics;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Logs;
    using Microsoft.AspNetCore.HttpOverrides;
    using System.Net;

    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddOptions<DotisanSecurityOptions>()
        .BindConfiguration("Dotisan:Security")
        .ValidateDataAnnotations()
        .ValidateOnStart();
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        foreach (var value in builder.Configuration.GetSection("Dotisan:Security:KnownProxies").Get<string[]>() ?? [])
        {
            if (IPAddress.TryParse(value, out var address))
                options.KnownProxies.Add(address);
        }
    });
    builder.Services.AddCors(options => options.AddPolicy("frontend", policy =>
    {
        var frontendUrl = builder.Environment.IsDevelopment()
            ? builder.Configuration["FrontendUrl"]
            : builder.Configuration["Dotisan:Security:FrontendUrl"] ?? builder.Configuration["FrontendUrl"];
        if (Uri.TryCreate(frontendUrl, UriKind.Absolute, out var origin))
            policy.WithOrigins(origin.GetLeftPart(UriPartial.Authority)).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }));
    if (args.Contains("--dotisan-observability", StringComparer.OrdinalIgnoreCase))
    {
        builder.Configuration["OpenTelemetry:Enabled"] = "true";
    }
    builder.Services.AddOpenApi();
    builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? context.HttpContext.TraceIdentifier;
    });
    var openTelemetry = builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation())
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation());
    builder.Logging.AddOpenTelemetry(logging => logging.IncludeFormattedMessage = true);
    if (builder.Configuration.GetValue("OpenTelemetry:Enabled", false))
    {
        openTelemetry.UseOtlpExporter();
    }
    var connectionString = ResolveConnectionString(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required."), builder.Environment.ContentRootPath);
    builder.Services.AddDbContext<AppDbContext>(options =>
        {{DatabaseRegistration(database)}});
    builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
    if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
    {
        DotisanProductionConfiguration.Validate(builder.Configuration, builder.Environment.EnvironmentName, emailConfirmationEnabled: false, dataProtectionEnabled: false);
    }
    builder.Services.AddScoped<IAuditWriter, AuditWriter>();
    {{(notificationsEnabled ? "builder.Services.AddSignalR();\n    builder.Services.AddSingleton<IUserIdProvider, NotificationUserIdProvider>();\n    builder.Services.AddScoped<INotificationStore, EfNotificationStore>();" : string.Empty)}}
    {{(storageEnabled ? "builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();" : string.Empty)}}
    {{(cachingEnabled ? "builder.Services.AddMemoryCache();\n    builder.Services.AddSingleton<IDistributedApplicationCache, MemoryApplicationCache>();\n    builder.Services.AddSingleton<IApplicationCache>(services => services.GetRequiredService<IDistributedApplicationCache>());" : string.Empty)}}
    {{(importsExportsEnabled ? "builder.Services.AddScoped<IDataExchangeService, DataExchangeService>();" : string.Empty)}}
    {{(webhooksEnabled ? "builder.Services.AddHttpClient(client => client.Timeout = TimeSpan.FromSeconds(30));\n    builder.Services.AddScoped<IWebhookDispatcher, HmacWebhookDispatcher>();" : string.Empty)}}
    {{(multiTenancyEnabled ? "builder.Services.AddHttpContextAccessor();\n    builder.Services.AddScoped<ITenantContext, TenantContext>();" : string.Empty)}}
    {{(jobsEnabled ? "builder.Host.UseWolverine(opts => JobRegistration.Configure(opts, connectionString, builder.Configuration));" : string.Empty)}}

    var app = builder.Build();
    app.UseForwardedHeaders();
    app.Use(async (context, next) =>
    {
        var supplied = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        var correlationId = !string.IsNullOrWhiteSpace(supplied) && supplied.Length <= 100 && supplied.All(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            ? supplied
            : Guid.NewGuid().ToString("N");
        context.Request.Headers["X-Correlation-ID"] = correlationId;
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        await next(context);
    });
    app.UseExceptionHandler();
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.UseCors("frontend");
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }
    app.MapDotisanEndpoints();
    app.MapFallbackToFile("index.html");
    app.Run();

    static string ResolveConnectionString(string configured, string contentRoot)
    {
        if (!configured.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            return configured;
        var parts = configured.Split(';');
        var dataSource = parts[0]["Data Source=".Length..].Trim();
        if (Path.IsPathRooted(dataSource) || dataSource.Contains('|'))
            return configured;
        var resolvedPath = Path.Combine(contentRoot, dataSource);
        Directory.CreateDirectory(Path.GetDirectoryName(resolvedPath)!);
        parts[0] = $"Data Source={resolvedPath}";
        return string.Join(';', parts);
    }

    public partial class Program { }
    """;

    private static string AuthenticatedApiProgram(string identifier, DatabaseProvider database, bool registrationEnabled, bool multiTenancyEnabled, bool notificationsEnabled, bool storageEnabled, bool cachingEnabled, bool importsExportsEnabled, bool webhooksEnabled, bool jobsEnabled) => $$"""
    using System.Security.Claims;
    using {{identifier}}.Api.Authorization;
    using {{identifier}}.Api.Auditing;
    using {{identifier}}.Api.Data;
    using {{identifier}}.Api.Identity;
    using {{identifier}}.Api.Integrations;
    using {{identifier}}.Api.Infrastructure;
    {{(jobsEnabled ? $"using {identifier}.Api.Jobs;" : string.Empty)}}
    using {{identifier}}.Api.Features.Health;
    using Microsoft.AspNetCore.DataProtection;
    {{(multiTenancyEnabled ? $"using {identifier}.Api.Tenancy;" : string.Empty)}}
    using Wolverine;
    using Microsoft.AspNetCore.Authentication.Cookies;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.RateLimiting;
    using Microsoft.AspNetCore.HttpOverrides;
    using System.Net;
    using Microsoft.EntityFrameworkCore;
    using System.Threading.RateLimiting;
    using OpenTelemetry;
    using OpenTelemetry.Metrics;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Logs;

    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddOptions<DotisanSecurityOptions>()
        .BindConfiguration("Dotisan:Security")
        .ValidateDataAnnotations()
        .ValidateOnStart();
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        foreach (var value in builder.Configuration.GetSection("Dotisan:Security:KnownProxies").Get<string[]>() ?? [])
        {
            if (IPAddress.TryParse(value, out var address))
                options.KnownProxies.Add(address);
        }
    });
    builder.Services.AddCors(options => options.AddPolicy("frontend", policy =>
    {
        var frontendUrl = builder.Environment.IsDevelopment()
            ? builder.Configuration["FrontendUrl"]
            : builder.Configuration["Dotisan:Security:FrontendUrl"] ?? builder.Configuration["FrontendUrl"];
        if (Uri.TryCreate(frontendUrl, UriKind.Absolute, out var origin))
            policy.WithOrigins(origin.GetLeftPart(UriPartial.Authority)).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }));
    if (args.Contains("--dotisan-observability", StringComparer.OrdinalIgnoreCase))
    {
        builder.Configuration["OpenTelemetry:Enabled"] = "true";
    }
    builder.Services.AddOpenApi();
    builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? context.HttpContext.TraceIdentifier;
    });
    var openTelemetry = builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation())
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation());
    builder.Logging.AddOpenTelemetry(logging => logging.IncludeFormattedMessage = true);
    if (builder.Configuration.GetValue("OpenTelemetry:Enabled", false))
    {
        openTelemetry.UseOtlpExporter();
    }
    var connectionString = ResolveConnectionString(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required."), builder.Environment.ContentRootPath);
    builder.Services.AddDbContext<AppDbContext>(options =>
        {{DatabaseRegistration(database)}});
    builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
    if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
    {
        DotisanProductionConfiguration.Validate(builder.Configuration, builder.Environment.EnvironmentName, emailConfirmationEnabled: true);
        var keyDirectory = builder.Configuration["Dotisan:Security:DataProtectionKeyDirectory"] ?? builder.Configuration["DataProtection:KeyDirectory"];
        var resolvedKeyDirectory = Path.GetFullPath(keyDirectory!, builder.Environment.ContentRootPath);
        Directory.CreateDirectory(resolvedKeyDirectory);
        builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(resolvedKeyDirectory));
    }
    builder.Services.AddScoped<IAuditWriter, AuditWriter>();
    {{(notificationsEnabled ? "builder.Services.AddSignalR();\n    builder.Services.AddSingleton<IUserIdProvider, NotificationUserIdProvider>();\n    builder.Services.AddScoped<INotificationStore, EfNotificationStore>();" : string.Empty)}}
    {{(storageEnabled ? "builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();" : string.Empty)}}
    {{(cachingEnabled ? "builder.Services.AddMemoryCache();\n    builder.Services.AddSingleton<IDistributedApplicationCache, MemoryApplicationCache>();\n    builder.Services.AddSingleton<IApplicationCache>(services => services.GetRequiredService<IDistributedApplicationCache>());" : string.Empty)}}
    {{(importsExportsEnabled ? "builder.Services.AddScoped<IDataExchangeService, DataExchangeService>();" : string.Empty)}}
    {{(webhooksEnabled ? "builder.Services.AddHttpClient(client => client.Timeout = TimeSpan.FromSeconds(30));\n    builder.Services.AddScoped<IWebhookDispatcher, HmacWebhookDispatcher>();" : string.Empty)}}
    {{(multiTenancyEnabled ? "builder.Services.AddHttpContextAccessor();\n    builder.Services.AddScoped<ITenantContext, TenantContext>();" : string.Empty)}}
    {{(jobsEnabled ? "builder.Host.UseWolverine(opts => JobRegistration.Configure(opts, connectionString, builder.Configuration));" : string.Empty)}}
    builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddRoles<IdentityRole>()
    .AddSignInManager()
    .AddDefaultTokenProviders()
    .AddEntityFrameworkStores<AppDbContext>();
    builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
    builder.Services.AddHttpClient();
    builder.Services.AddSingleton<IExternalLoginStateStore, ExternalLoginStateStore>();
    var mailProvider = builder.Configuration["Mail:Provider"]?.ToLowerInvariant() ?? "console";
    if (mailProvider == "mailpit" && !builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Mailpit is only supported in the Development environment. Select smtp for staging or production.");
    if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing") && mailProvider == "console")
        throw new InvalidOperationException("Mail:Provider must be smtp or a custom provider outside Development.");
    builder.Services.AddSingleton<IEmailProvider>(services =>
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        return mailProvider switch
        {
            "mailpit" => new MailpitEmailSender(services.GetRequiredService<IHttpClientFactory>(), configuration),
            "smtp" => new SmtpEmailSender(configuration),
            _ => new ConsoleEmailSender()
        };
    });
    builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
        .AddCookie(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
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
            options.Events.OnValidatePrincipal = async context =>
            {
                var value = context.Principal?.FindFirstValue("dotisan_session_id");
                if (!Guid.TryParse(value, out var sessionId)) return;
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var session = await db.ApplicationSessions.SingleOrDefaultAsync(item => item.Id == sessionId);
                var now = DateTimeOffset.UtcNow;
                if (session is null || session.RevokedAt is not null || session.ExpiresAt <= now) { context.RejectPrincipal(); return; }
                if (session.LastSeenAt < now.AddMinutes(-5)) { session.LastSeenAt = now; await db.SaveChangesAsync(); }
            };
        })
        .AddCookie(IdentityConstants.TwoFactorUserIdScheme)
        .AddCookie(IdentityConstants.TwoFactorRememberMeScheme);
    builder.Services.AddAuthorization(options =>
    {
        foreach (var permission in Permissions.All)
        {
            options.AddPolicy(permission, policy =>
                policy.RequireClaim(Permissions.ClaimType, permission));
        }
    });
    builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");
    builder.Services.AddRateLimiter(options => options.AddPolicy("account", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));

    var app = builder.Build();
    app.UseForwardedHeaders();
    app.Use(async (context, next) =>
    {
        var supplied = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        var correlationId = !string.IsNullOrWhiteSpace(supplied) && supplied.Length <= 100 && supplied.All(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            ? supplied
            : Guid.NewGuid().ToString("N");
        context.Request.Headers["X-Correlation-ID"] = correlationId;
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        await next(context);
    });
    app.UseExceptionHandler();
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.UseCors("frontend");
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();
    app.UseAntiforgery();
    app.MapDotisanEndpoints();
    app.MapFallbackToFile("index.html");
    app.Run();

    static string ResolveConnectionString(string configured, string contentRoot)
    {
        if (!configured.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            return configured;
        var parts = configured.Split(';');
        var dataSource = parts[0]["Data Source=".Length..].Trim();
        if (Path.IsPathRooted(dataSource) || dataSource.Contains('|'))
            return configured;
        var resolvedPath = Path.Combine(contentRoot, dataSource);
        Directory.CreateDirectory(Path.GetDirectoryName(resolvedPath)!);
        parts[0] = $"Data Source={resolvedPath}";
        return string.Join(';', parts);
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
        _ => $"This project uses EF Core SQLite. SQLite is file-based and needs no separate database service; the project-specific default connection string is `Data Source=Data/{name}.db`.",
    };

    private static string DatabaseRegistration(DatabaseProvider database) => database switch
    {
        DatabaseProvider.SqlServer => "options.UseSqlServer(connectionString)",
        DatabaseProvider.PostgreSQL => "options.UseNpgsql(connectionString)",
        DatabaseProvider.MySQL => "options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))",
        _ => "options.UseSqlite(connectionString)"
    };

    private static string AppSettings(string name, DatabaseProvider database, int webPort, bool jobsEnabled)
    {
        var connectionString = database switch
        {
            DatabaseProvider.SqlServer => $"Server=localhost,1433;Database={name};User Id=sa;Password=DotisanDev123!;TrustServerCertificate=True",
            DatabaseProvider.PostgreSQL => $"Host=localhost;Database={name.ToLowerInvariant()};Username=postgres;Password=postgres",
            DatabaseProvider.MySQL => $"Server=localhost;Database={name.ToLowerInvariant()};User=root;Password=root",
            _ => $"Data Source=Data/{name}.db"
        };

        return $$"""
        {
          "ConnectionStrings": {
            "DefaultConnection": "{{connectionString}}"
          },
          "Audit": {
            "Enabled": true
          },
          "OpenTelemetry": {
            "Enabled": false
          },
          "Dotisan": {
            "Security": {
              "FrontendUrl": "https://localhost:{{webPort}}",
              "DataProtectionKeyDirectory": "DataProtection-Keys",
              "KnownProxies": []
            },
              "Jobs": {
              "Enabled": {{jobsEnabled.ToString().ToLowerInvariant()}},
              "MaxAttempts": 3,
              "RetryDelaySeconds": 5
            }
          },
          "FrontendUrl": "http://localhost:{{webPort}}",
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

    private static string DevelopmentAppSettings(MailProvider provider) => $$"""
    {
      "Dotisan": {
        "Jobs": { "Enabled": false }
      },
      "Mail": {
        "Provider": "{{provider.ToString().ToLowerInvariant()}}",
        "Mailpit": { "BaseUrl": "http://localhost:8025/api/v1", "From": "no-reply@localhost" },
        "Smtp": { "Host": "localhost", "Port": 25, "From": "no-reply@localhost" }
      },
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft.AspNetCore": "Information"
        }
      }
    }
    """;


    private static string DatabaseCompose(string name, DatabaseProvider database, MailProvider mailProvider)
    {
        var databaseName = name.ToLowerInvariant();
        var volumeName = $"{databaseName}-database-data";

        var compose = database switch
        {
            DatabaseProvider.SqlServer => $$"""
            services:
              database:
                image: mcr.microsoft.com/mssql/server:2022-CU16-ubuntu-22.04
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
            _ => "services:\n"
        };

        var withMailpit = mailProvider == MailProvider.Mailpit
            ? compose + $$"""

              mailpit:
                image: axllent/mailpit:v1.21.8
                ports:
                  - "1025:1025"
                  - "8025:8025"
                healthcheck:
                  test: ["CMD", "wget", "--spider", "-q", "http://localhost:8025/api/v1/info"]
                  interval: 5s
                  timeout: 5s
                  retries: 20
            """
            : compose;

        return withMailpit + $$"""

              dashboard:
                image: mcr.microsoft.com/dotnet/aspire-dashboard:9.4
                ports:
                  - "18888:18888"
                  - "4317:18889"
                  - "4318:18890"
            """;
    }

    private static string TenantContext(string identifier) => $$"""
    using System.Security.Claims;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Hosting;

    namespace {{identifier}}.Api.Tenancy;

    public interface ITenantContext
    {
        string TenantId { get; }
        string RequireTenantId();
    }

    public interface ITenantEntity
    {
        string TenantId { get; set; }
    }

    public sealed class TenantContext(IHttpContextAccessor httpContextAccessor, IHostEnvironment environment) : ITenantContext
    {
        public string TenantId
        {
            get
            {
                var claimTenant = httpContextAccessor.HttpContext?.User.FindFirstValue("tenant_id")?.Trim();
                if (!string.IsNullOrWhiteSpace(claimTenant))
                    return claimTenant;

                var headerTenant = environment.IsDevelopment()
                    ? httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-ID"].FirstOrDefault()?.Trim()
                    : null;
                return !string.IsNullOrWhiteSpace(headerTenant)
                    ? headerTenant
                    : environment.IsEnvironment("Testing")
                        ? "test-tenant"
                    : throw new InvalidOperationException("A tenant_id claim is required.");
            }
        }

        public string RequireTenantId() => TenantId;
    }
    """;

    private static string DbContext(string identifier, bool authenticationEnabled, bool notificationsEnabled, bool webhooksEnabled, bool importsExportsEnabled) => authenticationEnabled
        ? IdentityDbContext(identifier, notificationsEnabled, webhooksEnabled, importsExportsEnabled)
        : PlainDbContext(identifier, notificationsEnabled, webhooksEnabled, importsExportsEnabled);

    private static string PlainDbContext(string identifier, bool notificationsEnabled, bool webhooksEnabled, bool importsExportsEnabled) => $$"""
    using {{identifier}}.Api.Auditing;
    {{(notificationsEnabled || webhooksEnabled || importsExportsEnabled ? $"using {identifier}.Api.Integrations;" : string.Empty)}}
    using Microsoft.EntityFrameworkCore;

    namespace {{identifier}}.Api.Data;

    public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
        {{(notificationsEnabled ? $"public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();" : string.Empty)}}
        {{(webhooksEnabled ? $"public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();" : string.Empty)}}
        {{(importsExportsEnabled ? $"public DbSet<ImportRecord> ImportRecords => Set<ImportRecord>();" : string.Empty)}}
    }
    """;

    private static string IdentityDbContext(string identifier, bool notificationsEnabled, bool webhooksEnabled, bool importsExportsEnabled) => $$"""
    using {{identifier}}.Api.Auditing;
    {{(notificationsEnabled || webhooksEnabled || importsExportsEnabled ? $"using {identifier}.Api.Integrations;" : string.Empty)}}
    using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore;
    using {{identifier}}.Api.Identity;

    namespace {{identifier}}.Api.Data;

    public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
        public DbSet<ApplicationSession> ApplicationSessions => Set<ApplicationSession>();
        {{(notificationsEnabled ? $"public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();" : string.Empty)}}
        {{(webhooksEnabled ? $"public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();" : string.Empty)}}
        {{(importsExportsEnabled ? $"public DbSet<ImportRecord> ImportRecords => Set<ImportRecord>();" : string.Empty)}}
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

    private static string AuditWriter(string identifier, bool multiTenancyEnabled) => $$"""
    using System.Diagnostics;
    using System.Security.Claims;
    using System.Text.Json;
    using {{identifier}}.Api.Data;
    {{(multiTenancyEnabled ? $"using {identifier}.Api.Tenancy;" : string.Empty)}}
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;

    namespace {{identifier}}.Api.Auditing;

    public sealed class AuditWriter(AppDbContext db, IConfiguration configuration{{(multiTenancyEnabled ? ", ITenantContext tenantContext" : string.Empty)}}) : IAuditWriter
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
                TenantId = {{(multiTenancyEnabled ? "tenantContext.TenantId" : "null")}},
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

    private static string ApplicationSession(string identifier) => $$"""
    namespace {{identifier}}.Api.Identity;

    public sealed class ApplicationSession
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset LastSeenAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? RevokedAt { get; set; }
        public string DeviceName { get; set; } = "Unknown device";
        public string? UserAgent { get; set; }
        public string? IpAddress { get; set; }
    }
    """;

    private static string EmailSender(string identifier) => $$"""
    namespace {{identifier}}.Api.Integrations;

    public interface IEmailProvider
    {
        Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default);
    }

    public sealed class MailpitEmailSender(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IEmailProvider
    {
        public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
        {
            var baseUrl = configuration["Mail:Mailpit:BaseUrl"] ?? "http://localhost:8025/api/v1";
            var sender = configuration["Mail:Mailpit:From"] ?? "no-reply@localhost";
            using var response = await httpClientFactory.CreateClient().PostAsJsonAsync($"{baseUrl.TrimEnd('/')}/send", new { From = new { Email = sender }, To = new[] { new { Email = recipient } }, Subject = subject, Text = body }, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }

    public sealed class SmtpEmailSender(IConfiguration configuration) : IEmailProvider
    {
        public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
        {
            using var client = new System.Net.Mail.SmtpClient(configuration["Mail:Smtp:Host"] ?? throw new InvalidOperationException("Mail:Smtp:Host is required."), configuration.GetValue("Mail:Smtp:Port", 25));
            client.EnableSsl = configuration.GetValue("Mail:Smtp:EnableSsl", true);
            client.Credentials = new System.Net.NetworkCredential(configuration["Mail:Smtp:Username"], configuration["Mail:Smtp:Password"]);
            using var message = new System.Net.Mail.MailMessage(configuration["Mail:Smtp:From"] ?? throw new InvalidOperationException("Mail:Smtp:From is required."), recipient, subject, body);
            await client.SendMailAsync(message, cancellationToken);
        }
    }

    public sealed class ConsoleEmailSender : IEmailProvider
    {
        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"Application email to {recipient}: {subject}\n{body}");
            return Task.CompletedTask;
        }
    }
    """;

    private static string ExternalLoginProvider(string identifier) => $$"""
    using Microsoft.AspNetCore.DataProtection;
    using System.Collections.Concurrent;

    namespace {{identifier}}.Api.Integrations;

    public sealed record ExternalLoginProviderDescriptor(string Name, string DisplayName);
    public sealed record ExternalLoginIdentity(string ProviderKey, string Email, string? DisplayName);
    public interface IExternalLoginStateStore
    {
        string Create(string returnUrl);
        bool TryConsume(string state, out string returnUrl);
    }

    public sealed class ExternalLoginStateStore(IDataProtectionProvider dataProtectionProvider) : IExternalLoginStateStore
    {
        private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("Dotisan.ExternalLogin.State");
        private readonly ConcurrentDictionary<string, byte> consumed = new(StringComparer.Ordinal);

        public string Create(string returnUrl) => protector.Protect($"{Guid.NewGuid():N}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}|{returnUrl}");

        public bool TryConsume(string state, out string returnUrl)
        {
            returnUrl = "/";
            try
            {
                var parts = protector.Unprotect(state).Split('|', 3);
                if (parts.Length != 3 || !long.TryParse(parts[1], out var createdAt) || DateTimeOffset.UtcNow.ToUnixTimeSeconds() - createdAt > 600 || !consumed.TryAdd(parts[0], 0)) return false;
                returnUrl = parts[2];
                return true;
            }
            catch (Exception) { return false; }
        }
    }

    public interface IExternalLoginProvider
    {
        string Name { get; }
        string DisplayName { get; }
        Task<string> CreateChallengeUrlAsync(string returnUrl, CancellationToken cancellationToken = default);
        Task<ExternalLoginIdentity?> ResolveIdentityAsync(string callbackCode, string state, CancellationToken cancellationToken = default);
    }
    """;

    private static string IntegrationExamples(string identifier) => $$"""
    using Microsoft.AspNetCore.DataProtection;
    using System.Net.Http.Headers;
    using System.Text.Json;

    namespace {{identifier}}.Api.Integrations;

    // Optional examples. Register only after supplying secrets through deployment configuration.
    public sealed class SendGridEmailProvider(IHttpClientFactory clients, IConfiguration configuration) : IEmailProvider
    {
        public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration["Mail:SendGrid:ApiKey"] ?? throw new InvalidOperationException("Mail:SendGrid:ApiKey is required."));
            request.Content = JsonContent.Create(new { personalizations = new[] { new { to = new[] { new { email = recipient } } } }, from = new { email = configuration["Mail:SendGrid:From"] ?? throw new InvalidOperationException("Mail:SendGrid:From is required.") }, subject, content = new[] { new { type = "text/plain", value = body } } });
            (await clients.CreateClient().SendAsync(request, cancellationToken)).EnsureSuccessStatusCode();
        }
    }

    public sealed class MailgunEmailProvider(IHttpClientFactory clients, IConfiguration configuration) : IEmailProvider
    {
        public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
        {
            var domain = configuration["Mail:Mailgun:Domain"] ?? throw new InvalidOperationException("Mail:Mailgun:Domain is required.");
            var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.mailgun.net/v3/{domain}/messages") { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["from"] = configuration["Mail:Mailgun:From"] ?? throw new InvalidOperationException("Mail:Mailgun:From is required."), ["to"] = recipient, ["subject"] = subject, ["text"] = body }) };
            var key = configuration["Mail:Mailgun:ApiKey"] ?? throw new InvalidOperationException("Mail:Mailgun:ApiKey is required.");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"api:{key}")));
            (await clients.CreateClient().SendAsync(request, cancellationToken)).EnsureSuccessStatusCode();
        }
    }

    public sealed class ConfiguredOAuth2Provider(IHttpClientFactory clients, IConfiguration configuration, IExternalLoginStateStore stateStore) : IExternalLoginProvider
    {
        public string Name => configuration["OAuth:Name"] ?? "oauth2";
        public string DisplayName => configuration["OAuth:DisplayName"] ?? "External provider";
        public Task<string> CreateChallengeUrlAsync(string returnUrl, CancellationToken cancellationToken = default)
        {
            var state = stateStore.Create(returnUrl);
            var query = $"client_id={Uri.EscapeDataString(configuration["OAuth:ClientId"] ?? "")}&redirect_uri={Uri.EscapeDataString(configuration["OAuth:RedirectUri"] ?? "")}&response_type=code&scope={Uri.EscapeDataString(configuration["OAuth:Scope"] ?? "openid profile email")}&state={Uri.EscapeDataString(state)}";
            return Task.FromResult($"{configuration["OAuth:AuthorizationEndpoint"]}?{query}");
        }
        public async Task<ExternalLoginIdentity?> ResolveIdentityAsync(string callbackCode, string state, CancellationToken cancellationToken = default)
        {
            var tokenResponse = await clients.CreateClient().PostAsync(configuration["OAuth:TokenEndpoint"], new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = callbackCode, ["client_id"] = configuration["OAuth:ClientId"] ?? "", ["client_secret"] = configuration["OAuth:ClientSecret"] ?? "", ["redirect_uri"] = configuration["OAuth:RedirectUri"] ?? "", ["grant_type"] = "authorization_code" }), cancellationToken);
            tokenResponse.EnsureSuccessStatusCode(); var token = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("access_token").GetString();
            using var profileRequest = new HttpRequestMessage(HttpMethod.Get, configuration["OAuth:UserInfoEndpoint"]); profileRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token); using var profileResponse = await clients.CreateClient().SendAsync(profileRequest, cancellationToken); profileResponse.EnsureSuccessStatusCode(); var profile = await profileResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken); var key = profile.GetProperty(configuration["OAuth:SubjectClaim"] ?? "sub").GetString(); var email = profile.GetProperty(configuration["OAuth:EmailClaim"] ?? "email").GetString();
            return key is null || email is null ? null : new ExternalLoginIdentity(key, email, profile.TryGetProperty(configuration["OAuth:NameClaim"] ?? "name", out var name) ? name.GetString() : null);
        }
    }
    """;

    private static string AccountEndpoints(string identifier, bool registrationEnabled) => $$"""
    using System.Security.Claims;
    using {{identifier}}.Api.Auditing;
    using {{identifier}}.Api.Data;
    using {{identifier}}.Api.Identity;
    using {{identifier}}.Api.Integrations;
    using Microsoft.AspNetCore.Authentication.Cookies;
    using Microsoft.AspNetCore.Antiforgery;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.EntityFrameworkCore;

    namespace {{identifier}}.Api.Features.Account;

    public static class AccountEndpoints
    {
        public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints, bool registrationEnabled)
        {
            var group = endpoints.MapGroup("/api/account").RequireRateLimiting("account");
            group.MapGet("/antiforgery", IssueAntiforgery).AllowAnonymous();
            group.MapPost("/register", (RegisterRequest request, UserManager<ApplicationUser> users, IEmailProvider emailProvider, HttpContext httpContext, IAuditWriter audit, IConfiguration configuration, CancellationToken cancellationToken) => Register(request, users, emailProvider, audit, httpContext, configuration, registrationEnabled, cancellationToken)).AllowAnonymous().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapPost("/login", (LoginRequest request, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signInManager, AppDbContext db, HttpContext httpContext, IAuditWriter audit, CancellationToken cancellationToken) => Login(request, users, signInManager, db, audit, httpContext, cancellationToken)).AllowAnonymous().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapPost("/password-reset/request", (PasswordResetRequest request, UserManager<ApplicationUser> users, IEmailProvider emailProvider, CancellationToken cancellationToken) => RequestPasswordReset(request, users, emailProvider, cancellationToken)).AllowAnonymous().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapPost("/password-reset/confirm", (PasswordResetConfirmRequest request, UserManager<ApplicationUser> users, CancellationToken cancellationToken) => ConfirmPasswordReset(request, users, cancellationToken)).AllowAnonymous().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapPost("/email-confirmation/confirm", (EmailConfirmationRequest request, UserManager<ApplicationUser> users, CancellationToken cancellationToken) => ConfirmEmail(request, users, cancellationToken)).AllowAnonymous().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapPost("/email-confirmation/resend", (EmailConfirmationResendRequest request, UserManager<ApplicationUser> users, IEmailProvider emailProvider, IConfiguration configuration, CancellationToken cancellationToken) => ResendConfirmation(request, users, emailProvider, configuration, cancellationToken)).AllowAnonymous().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapGet("/mfa/setup", (ClaimsPrincipal principal, UserManager<ApplicationUser> users) => SetupMfa(principal, users)).RequireAuthorization();
            group.MapPost("/mfa/verify", (MfaCodeRequest request, ClaimsPrincipal principal, UserManager<ApplicationUser> users) => VerifyMfa(request, principal, users)).RequireAuthorization().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapPost("/mfa/disable", (ClaimsPrincipal principal, UserManager<ApplicationUser> users) => DisableMfa(principal, users)).RequireAuthorization().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapPost("/mfa/recovery-codes/regenerate", (ClaimsPrincipal principal, UserManager<ApplicationUser> users) => RegenerateRecoveryCodes(principal, users)).RequireAuthorization().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapPost("/mfa/challenge", (MfaCodeRequest request, SignInManager<ApplicationUser> signInManager, AppDbContext db, HttpContext httpContext, CancellationToken cancellationToken) => ChallengeMfa(request, signInManager, db, httpContext, cancellationToken)).AllowAnonymous().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapGet("/sessions", (ClaimsPrincipal principal, AppDbContext db) => ListSessions(principal, db)).RequireAuthorization();
            group.MapDelete("/sessions/{id:guid}", (Guid id, ClaimsPrincipal principal, AppDbContext db) => RevokeSession(id, principal, db)).RequireAuthorization().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapPost("/sessions/revoke-all", (ClaimsPrincipal principal, UserManager<ApplicationUser> users, AppDbContext db) => RevokeAllSessions(principal, users, db)).RequireAuthorization().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapGet("/external/providers", (IEnumerable<IExternalLoginProvider> providers) => Results.Ok(new { providers = providers.Select(provider => new ExternalLoginProviderDescriptor(provider.Name, provider.DisplayName)) })).AllowAnonymous();
            group.MapGet("/external/{provider}/challenge", (string provider, string? returnUrl, IEnumerable<IExternalLoginProvider> providers, CancellationToken cancellationToken) => ExternalLoginChallenge(provider, returnUrl, providers, cancellationToken)).AllowAnonymous();
            group.MapGet("/external/{provider}/callback", (string provider, string code, string state, ClaimsPrincipal principal, IEnumerable<IExternalLoginProvider> providers, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signInManager, AppDbContext db, HttpContext httpContext, IExternalLoginStateStore stateStore, CancellationToken cancellationToken) => ExternalLoginCallback(provider, code, state, principal, providers, users, signInManager, db, httpContext, stateStore, cancellationToken)).AllowAnonymous();
            group.MapPost("/external/{provider}/link", (string provider, string? returnUrl, ClaimsPrincipal principal, IEnumerable<IExternalLoginProvider> providers, CancellationToken cancellationToken) => ExternalLoginChallenge(provider, returnUrl, providers, cancellationToken)).RequireAuthorization().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapDelete("/external/{provider}/link", (string provider, ClaimsPrincipal principal, IEnumerable<IExternalLoginProvider> providers, UserManager<ApplicationUser> users) => UnlinkExternalLogin(provider, principal, providers, users)).RequireAuthorization().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapPost("/logout", (SignInManager<ApplicationUser> signInManager, HttpContext httpContext, IAuditWriter audit, CancellationToken cancellationToken) => Logout(signInManager, audit, httpContext, cancellationToken)).RequireAuthorization().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
            group.MapGet("/me", (ClaimsPrincipal user) => Me(user)).RequireAuthorization();
            return endpoints;
        }

        private static IResult IssueAntiforgery(HttpContext httpContext, IAntiforgery antiforgery)
        {
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return Results.Ok(new { token = tokens.RequestToken });
        }

        private static async Task<IResult> Register(RegisterRequest request, UserManager<ApplicationUser> users, IEmailProvider emailProvider, IAuditWriter audit, HttpContext httpContext, IConfiguration configuration, bool registrationEnabled, CancellationToken cancellationToken)
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

            var confirmationToken = await users.GenerateEmailConfirmationTokenAsync(user);
            await emailProvider.SendAsync(user.Email!, "Confirm your email", BuildConfirmationEmail(configuration["Dotisan:Security:FrontendUrl"] ?? configuration["FrontendUrl"] ?? "http://localhost:5173", user.Email!, confirmationToken), cancellationToken);
            await audit.RecordAsync(httpContext, "Security", user.Id, "security.registered", new Dictionary<string, object?>(), cancellationToken);
            return Results.Ok(new CurrentUserResponse(user.Id, user.Email!));
        }

        private static async Task<IResult> Login(LoginRequest request, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signInManager, AppDbContext db, IAuditWriter audit, HttpContext httpContext, CancellationToken cancellationToken)
        {
            var user = await users.FindByEmailAsync(request.Email);
            if (user is null)
            {
                await audit.RecordAsync(httpContext, "Security", null, "security.login.failed", new Dictionary<string, object?>(), cancellationToken);
                return Results.Unauthorized();
            }

            if (!await users.IsEmailConfirmedAsync(user))
            {
                await audit.RecordAsync(httpContext, "Security", user.Id, "security.login.unconfirmed_email", new Dictionary<string, object?>(), cancellationToken);
                return Results.Unauthorized();
            }

            signInManager.AuthenticationScheme = IdentityConstants.ApplicationScheme;
            var result = await signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);
            if (result.RequiresTwoFactor)
                return Results.Ok(new LoginResponse("mfa_required"));
            if (!result.Succeeded)
            {
                await audit.RecordAsync(httpContext, "Security", user.Id, "security.login.failed", new Dictionary<string, object?>(), cancellationToken);
                return Results.Unauthorized();
            }

            await audit.RecordAsync(httpContext, "Security", user.Id, "security.login.succeeded", new Dictionary<string, object?>(), cancellationToken);
            var sessionId = await CreateSession(user.Id, db, httpContext, cancellationToken);
            await signInManager.SignInWithClaimsAsync(user, request.RememberMe, [new Claim("dotisan_session_id", sessionId.ToString())]);
            return Results.Ok(new LoginResponse("authenticated"));
        }

        private static async Task<Guid> CreateSession(string userId, AppDbContext db, HttpContext httpContext, CancellationToken cancellationToken)
        {
            var agent = httpContext.Request.Headers.UserAgent.ToString();
            var now = DateTimeOffset.UtcNow;
            var id = Guid.NewGuid();
            db.ApplicationSessions.Add(new ApplicationSession { Id = id, UserId = userId, CreatedAt = now, LastSeenAt = now, ExpiresAt = now.AddDays(30), DeviceName = string.IsNullOrWhiteSpace(agent) ? "Unknown device" : agent[..Math.Min(agent.Length, 100)], UserAgent = agent, IpAddress = httpContext.Connection.RemoteIpAddress?.ToString() });
            await db.SaveChangesAsync(cancellationToken);
            return id;
        }

        private static async Task<IResult> ListSessions(ClaimsPrincipal principal, AppDbContext db)
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();
            var sessions = (await db.ApplicationSessions.AsNoTracking().Where(session => session.UserId == userId && session.RevokedAt == null).ToListAsync()).Where(session => session.ExpiresAt > DateTimeOffset.UtcNow).OrderByDescending(session => session.LastSeenAt).Select(session => new { session.Id, session.DeviceName, session.CreatedAt, session.LastSeenAt, session.ExpiresAt, session.UserAgent, session.IpAddress }).ToList();
            return Results.Ok(new { sessions });
        }

        private static async Task<IResult> RevokeSession(Guid id, ClaimsPrincipal principal, AppDbContext db)
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var session = userId is null ? null : await db.ApplicationSessions.SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId && item.RevokedAt == null);
            if (session is null) return Results.NotFound();
            session.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }

        private static async Task<IResult> RevokeAllSessions(ClaimsPrincipal principal, UserManager<ApplicationUser> users, AppDbContext db)
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            var sessions = await db.ApplicationSessions.Where(session => session.UserId == user.Id && session.RevokedAt == null).ToListAsync();
            foreach (var session in sessions) session.RevokedAt = DateTimeOffset.UtcNow;
            await users.UpdateSecurityStampAsync(user);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }

        private static async Task<IResult> ExternalLoginChallenge(string provider, string? returnUrl, IEnumerable<IExternalLoginProvider> providers, CancellationToken cancellationToken)
        {
            var adapter = providers.FirstOrDefault(item => string.Equals(item.Name, provider, StringComparison.OrdinalIgnoreCase));
            if (adapter is null) return Results.NotFound(new { code = "external_provider_not_configured" });
            var safeReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Uri.TryCreate(returnUrl, UriKind.Relative, out _) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal) ? returnUrl : "/";
            return Results.Redirect(await adapter.CreateChallengeUrlAsync(safeReturnUrl, cancellationToken));
        }

        private static async Task<IResult> ExternalLoginCallback(string provider, string code, string state, ClaimsPrincipal principal, IEnumerable<IExternalLoginProvider> providers, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signInManager, AppDbContext db, HttpContext httpContext, IExternalLoginStateStore stateStore, CancellationToken cancellationToken)
        {
            var adapter = providers.FirstOrDefault(item => string.Equals(item.Name, provider, StringComparison.OrdinalIgnoreCase));
            if (adapter is null) return Results.NotFound(new { code = "external_provider_not_configured" });
            if (!stateStore.TryConsume(state, out var returnUrl)) return Results.BadRequest(new { code = "invalid_external_state" });
            var identity = await adapter.ResolveIdentityAsync(code, state, cancellationToken);
            if (identity is null) return Results.BadRequest(new { code = "invalid_external_identity" });
            var login = new UserLoginInfo(adapter.Name, identity.ProviderKey, adapter.DisplayName);
            var user = principal.Identity?.IsAuthenticated == true ? await users.GetUserAsync(principal) : null;
            if (user is not null) { await users.AddLoginAsync(user, login); return Results.Redirect(returnUrl); }
            user = await users.FindByLoginAsync(adapter.Name, identity.ProviderKey);
            if (user is null) { user = await users.FindByEmailAsync(identity.Email); if (user is null) { user = new ApplicationUser { UserName = identity.Email, Email = identity.Email, EmailConfirmed = true }; var created = await users.CreateAsync(user); if (!created.Succeeded) return Results.BadRequest(new { code = "external_registration_failed" }); } await users.AddLoginAsync(user, login); }
            var sessionId = await CreateSession(user.Id, db, httpContext, cancellationToken);
            await signInManager.SignInWithClaimsAsync(user, false, [new Claim("dotisan_session_id", sessionId.ToString())]);
            return Results.Redirect(returnUrl);
        }

        private static async Task<IResult> UnlinkExternalLogin(string provider, ClaimsPrincipal principal, IEnumerable<IExternalLoginProvider> providers, UserManager<ApplicationUser> users)
        {
            var adapter = providers.FirstOrDefault(item => string.Equals(item.Name, provider, StringComparison.OrdinalIgnoreCase));
            var user = await users.GetUserAsync(principal);
            if (adapter is null) return Results.NotFound(new { code = "external_provider_not_configured" });
            if (user is null) return Results.Unauthorized();
            var linked = (await users.GetLoginsAsync(user)).FirstOrDefault(item => string.Equals(item.LoginProvider, adapter.Name, StringComparison.OrdinalIgnoreCase));
            if (linked is null) return Results.BadRequest(new { code = "external_login_not_linked" });
            var result = await users.RemoveLoginAsync(user, linked.LoginProvider, linked.ProviderKey);
            return result.Succeeded ? Results.NoContent() : Results.BadRequest(new { code = "external_login_not_linked" });
        }

        private static async Task<IResult> Logout(SignInManager<ApplicationUser> signInManager, IAuditWriter audit, HttpContext httpContext, CancellationToken cancellationToken)
        {
            signInManager.AuthenticationScheme = IdentityConstants.ApplicationScheme;
            await audit.RecordAsync(httpContext, "Security", null, "security.logout", new Dictionary<string, object?>(), cancellationToken);
            await signInManager.SignOutAsync();
            return Results.NoContent();
        }

        private static async Task<IResult> RequestPasswordReset(PasswordResetRequest request, UserManager<ApplicationUser> users, IEmailProvider emailProvider, CancellationToken cancellationToken)
        {
            var user = await users.FindByEmailAsync(request.Email);
            if (user is not null)
            {
                var token = await users.GeneratePasswordResetTokenAsync(user);
                await emailProvider.SendAsync(user.Email!, "Reset your password", $"Use this password reset token: {token}", cancellationToken);
            }
            return Results.Accepted();
        }

        private static async Task<IResult> ConfirmEmail(EmailConfirmationRequest request, UserManager<ApplicationUser> users, CancellationToken cancellationToken)
        {
            var user = await users.FindByEmailAsync(request.Email);
            if (user is null) return Results.BadRequest(new { code = "invalid_confirmation" });
            var result = await users.ConfirmEmailAsync(user, request.Token);
            return result.Succeeded ? Results.NoContent() : Results.BadRequest(new { code = "invalid_confirmation" });
        }

        private static async Task<IResult> ResendConfirmation(EmailConfirmationResendRequest request, UserManager<ApplicationUser> users, IEmailProvider emailProvider, IConfiguration configuration, CancellationToken cancellationToken)
        {
            var user = await users.FindByEmailAsync(request.Email);
            if (user is not null && !await users.IsEmailConfirmedAsync(user))
            {
                var token = await users.GenerateEmailConfirmationTokenAsync(user);
                await emailProvider.SendAsync(user.Email!, "Confirm your email", BuildConfirmationEmail(configuration["Dotisan:Security:FrontendUrl"] ?? configuration["FrontendUrl"] ?? "http://localhost:5173", user.Email!, token), cancellationToken);
            }
            return Results.Accepted();
        }

        private static string BuildConfirmationEmail(string frontendUrl, string email, string token)
        {
            var link = $"{frontendUrl.TrimEnd('/')}/auth/confirm-email?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
            return $"Welcome!\n\nPlease confirm your email address by opening this link:\n{link}\n\nIf you did not create this account, you can safely ignore this email.";
        }

        private static async Task<IResult> ConfirmPasswordReset(PasswordResetConfirmRequest request, UserManager<ApplicationUser> users, CancellationToken cancellationToken)
        {
            var user = await users.FindByEmailAsync(request.Email);
            if (user is null) return Results.BadRequest(new { code = "invalid_reset" });
            var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
            return result.Succeeded ? Results.NoContent() : Results.BadRequest(new { code = "invalid_reset" });
        }

        private static async Task<IResult> SetupMfa(ClaimsPrincipal principal, UserManager<ApplicationUser> users)
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            var key = await users.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrWhiteSpace(key)) { await users.ResetAuthenticatorKeyAsync(user); key = await users.GetAuthenticatorKeyAsync(user); }
            return Results.Ok(new { sharedKey = key, authenticatorUri = $"otpauth://totp/Dotisan:{Uri.EscapeDataString(user.Email!)}?secret={key}&issuer=Dotisan" });
        }

        private static async Task<IResult> VerifyMfa(MfaCodeRequest request, ClaimsPrincipal principal, UserManager<ApplicationUser> users)
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            if (!await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, request.Code)) return Results.BadRequest(new { code = "invalid_mfa_code" });
            await users.SetTwoFactorEnabledAsync(user, true);
            return Results.NoContent();
        }

        private static async Task<IResult> DisableMfa(ClaimsPrincipal principal, UserManager<ApplicationUser> users)
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            await users.SetTwoFactorEnabledAsync(user, false);
            return Results.NoContent();
        }

        private static async Task<IResult> RegenerateRecoveryCodes(ClaimsPrincipal principal, UserManager<ApplicationUser> users)
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            if (!await users.GetTwoFactorEnabledAsync(user)) return Results.BadRequest(new { code = "mfa_not_enabled" });
            return Results.Ok(new { recoveryCodes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10) });
        }

        private static async Task<IResult> ChallengeMfa(MfaCodeRequest request, SignInManager<ApplicationUser> signInManager, AppDbContext db, HttpContext httpContext, CancellationToken cancellationToken)
        {
            var result = await signInManager.TwoFactorAuthenticatorSignInAsync(request.Code, false, false);
            if (!result.Succeeded)
                result = await signInManager.TwoFactorRecoveryCodeSignInAsync(request.Code);
            if (result.Succeeded)
            {
                var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
                if (user is not null)
                {
                    var sessionId = await CreateSession(user.Id, db, httpContext, cancellationToken);
                    await signInManager.SignInWithClaimsAsync(user, false, [new Claim("dotisan_session_id", sessionId.ToString())]);
                }
            }
            return result.Succeeded ? Results.NoContent() : Results.Unauthorized();
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
        public sealed record LoginResponse(string Code);
        public sealed record PasswordResetRequest(string Email);
        public sealed record PasswordResetConfirmRequest(string Email, string Token, string NewPassword);
        public sealed record EmailConfirmationRequest(string Email, string Token);
        public sealed record EmailConfirmationResendRequest(string Email);
        public sealed record MfaCodeRequest(string Code);
        public sealed record CurrentUserResponse(string Id, string Email);
    }
    """;

    private static string EndpointExtensions(string identifier, bool authenticationEnabled, bool registrationEnabled, bool notificationsEnabled, bool storageEnabled, bool importsExportsEnabled, bool webhooksEnabled, bool jobsEnabled) => $$"""
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.EntityFrameworkCore;
    using {{identifier}}.Api.Features.Health;
    {{(jobsEnabled ? $"using {identifier}.Api.Features.Jobs;" : string.Empty)}}
    {{(notificationsEnabled || storageEnabled || importsExportsEnabled || webhooksEnabled ? $"using {identifier}.Api.Integrations;" : string.Empty)}}
    {{(authenticationEnabled ? $"using {identifier}.Api.Features.Account;\n    using {identifier}.Api.Features.Authorization;" : string.Empty)}}

    namespace {{identifier}}.Api.Infrastructure;

    public static class DotisanEndpointExtensions
    {
        // Endpoint registration is intentionally explicit and inspectable.
        public static IEndpointRouteBuilder MapDotisanEndpoints(this IEndpointRouteBuilder endpoints)
        {
            // DOTISAN:ENDPOINTS
            HealthEndpoints.MapHealthEndpoints(endpoints);
            {{(jobsEnabled ? "JobEndpoints.MapJobEndpoints(endpoints);" : string.Empty)}}
            {{(notificationsEnabled ? "NotificationEndpoints.Map(endpoints); endpoints.MapHub<NotificationHub>(\"/hubs/notifications\");" : string.Empty)}}
            {{(storageEnabled ? "StorageEndpoints.Map(endpoints);" : string.Empty)}}
            {{(importsExportsEnabled ? "ImportExportEndpoints.Map(endpoints);" : string.Empty)}}
            {{(webhooksEnabled ? "WebhookEndpoints.Map(endpoints);" : string.Empty)}}
            {{(authenticationEnabled ? $"AccountEndpoints.MapAccountEndpoints(endpoints, {registrationEnabled.ToString().ToLowerInvariant()});\n            AuthorizationEndpoints.MapAuthorizationEndpoints(endpoints);" : string.Empty)}}
            return endpoints;
        }
    }
    """;

    private static string ApiTestsProject(string name, bool authenticationEnabled) => $$"""
    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
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
        <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
        <PackageReference Include="Microsoft.Data.Sqlite" />
        <PackageReference Include="Microsoft.Extensions.Logging" />
        {{(authenticationEnabled ? "<PackageReference Include=\"Microsoft.EntityFrameworkCore.Sqlite\" />" : string.Empty)}}
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
    using System.Text.Json;
    using {{identifier}}.Api.Authorization;
    using {{identifier}}.Api.Auditing;
    using {{identifier}}.Api.Data;
    using {{identifier}}.Api.Identity;
    using {{identifier}}.Api.Integrations;
    using {{identifier}}.Api.Infrastructure;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Data.Sqlite;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.AspNetCore.Identity;

    namespace {{identifier}}.Api.Tests;

    public sealed class AuthenticationEndpointTests(AuthenticationApplicationFactory factory) : IClassFixture<AuthenticationApplicationFactory>
    {
        [Fact]
        public async Task Liveness_and_readiness_health_endpoints_are_available()
        {
            using var client = factory.CreateClient();
            var live = await client.GetAsync("/health/live");
            var ready = await client.GetAsync("/health/ready");
            Assert.Equal(HttpStatusCode.OK, live.StatusCode);
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        }

        [Fact]
        public async Task Correlation_id_is_returned_on_api_responses()
        {
            using var client = factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/account/me");
            request.Headers.Add("X-Correlation-ID", "health-correlation");
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("health-correlation", response.Headers.GetValues("X-Correlation-ID").Single());
        }

        [Fact]
        public void Production_configuration_rejects_missing_secure_settings()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mail:Provider"] = "console"
            }).Build();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                DotisanProductionConfiguration.Validate(configuration, "Production", emailConfirmationEnabled: true));

            Assert.Contains("FrontendUrl", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Production_configuration_accepts_explicit_deployment_settings()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Dotisan:Security:FrontendUrl"] = "https://app.example.com",
                ["Dotisan:Security:DataProtectionKeyDirectory"] = "keys",
                ["Mail:Provider"] = "smtp"
            }).Build();

            DotisanProductionConfiguration.Validate(configuration, "Production", emailConfirmationEnabled: true);
        }

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
        public async Task Newly_registered_unconfirmed_users_cannot_access_protected_resources()
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
            var antiforgery = await GetAntiforgeryToken(client);
            using var register = new HttpRequestMessage(HttpMethod.Post, "/api/account/register");
            register.Headers.Add("X-XSRF-TOKEN", antiforgery);
            register.Content = JsonContent.Create(new { email = $"unconfirmed-{Guid.NewGuid():N}@example.com", password = "Password1!" });

            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(register)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);
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

        [Fact]
        public async Task Mfa_setup_requires_authentication_and_invalid_codes_are_rejected()
        {
            using var anonymous = factory.CreateClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/account/mfa/setup")).StatusCode);
            using var client = await SignInAsync($"mfa-{Guid.NewGuid():N}@example.com");
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/account/mfa/setup")).StatusCode);
            var antiforgery = await GetAntiforgeryToken(client);
            using var verify = new HttpRequestMessage(HttpMethod.Post, "/api/account/mfa/verify");
            verify.Headers.Add("X-XSRF-TOKEN", antiforgery);
            verify.Content = JsonContent.Create(new { code = "000000" });
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(verify)).StatusCode);
        }

        [Fact]
        public async Task Recovery_code_completes_the_mfa_challenge_once()
        {
            var email = $"recovery-{Guid.NewGuid():N}@example.com";
            var recoveryCode = await factory.SeedMfaUserAsync(email);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
            var antiforgery = await GetAntiforgeryToken(client);
            using var login = new HttpRequestMessage(HttpMethod.Post, "/api/account/login");
            login.Headers.Add("X-XSRF-TOKEN", antiforgery);
            login.Content = JsonContent.Create(new { email, password = "Password1!", rememberMe = false });
            var loginResponse = await client.SendAsync(login);
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
            Assert.Contains("mfa_required", await loginResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            antiforgery = await GetAntiforgeryToken(client);
            using var challenge = new HttpRequestMessage(HttpMethod.Post, "/api/account/mfa/challenge");
            challenge.Headers.Add("X-XSRF-TOKEN", antiforgery);
            challenge.Content = JsonContent.Create(new { code = recoveryCode });
            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(challenge)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/account/me")).StatusCode);
        }

        [Fact]
        public async Task Session_can_be_listed_and_revoked()
        {
            using var client = await SignInAsync($"session-{Guid.NewGuid():N}@example.com");
            var sessionsResponse = await client.GetAsync("/api/account/sessions");
            Assert.True(sessionsResponse.IsSuccessStatusCode, await sessionsResponse.Content.ReadAsStringAsync());
            var payload = JsonSerializer.Deserialize<JsonElement>(await sessionsResponse.Content.ReadAsStringAsync());
            var sessionId = payload.GetProperty("sessions")[0].GetProperty("id").GetGuid();
            var antiforgery = await GetAntiforgeryToken(client);
            using var revoke = new HttpRequestMessage(HttpMethod.Delete, $"/api/account/sessions/{sessionId}");
            revoke.Headers.Add("X-XSRF-TOKEN", antiforgery);
            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(revoke)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);
        }

        [Fact]
        public async Task Email_confirmation_rejects_invalid_tokens_without_revealing_details()
        {
            using var client = factory.CreateClient();
            var antiforgery = await GetAntiforgeryToken(client);
            using var confirmation = new HttpRequestMessage(HttpMethod.Post, "/api/account/email-confirmation/confirm");
            confirmation.Headers.Add("X-XSRF-TOKEN", antiforgery);
            confirmation.Content = JsonContent.Create(new { email = "missing@example.com", token = "invalid" });
            var response = await client.SendAsync(confirmation);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("invalid_confirmation", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task External_provider_listing_is_safe_when_no_adapter_is_registered()
        {
            using var client = factory.CreateClient();
            var providers = await client.GetFromJsonAsync<JsonElement>("/api/account/external/providers");
            Assert.Empty(providers.GetProperty("providers").EnumerateArray());
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/account/external/google/challenge")).StatusCode);
        }

        [Fact]
        public void External_login_state_is_protected_and_single_use()
        {
            var stateStore = factory.Services.GetRequiredService<IExternalLoginStateStore>();
            var state = stateStore.Create("/security/external-logins");

            Assert.DoesNotContain("/security/external-logins", state, StringComparison.Ordinal);
            Assert.True(stateStore.TryConsume(state, out var returnUrl));
            Assert.Equal("/security/external-logins", returnUrl);
            Assert.False(stateStore.TryConsume(state, out _));
            Assert.False(stateStore.TryConsume("invalid-state", out _));
        }

        [Fact]
        public async Task Fake_external_provider_completes_login_and_rejects_replayed_state()
        {
            using var client = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IExternalLoginProvider, FakeExternalLoginProvider>()))
                .CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
            var challenge = await client.GetAsync("/api/account/external/fake/challenge?returnUrl=%2Fsecurity%2Fexternal-logins");
            Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
            var callbackUrl = challenge.Headers.Location?.ToString() ?? throw new InvalidOperationException("Fake provider did not return a callback URL.");

            var callback = await client.GetAsync(callbackUrl);
            Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
            Assert.Equal("/security/external-logins", callback.Headers.Location?.ToString());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/account/me")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(callbackUrl)).StatusCode);
        }

        private sealed class FakeExternalLoginProvider(IExternalLoginStateStore stateStore) : IExternalLoginProvider
        {
            public string Name => "fake";
            public string DisplayName => "Fake provider";
            public Task<string> CreateChallengeUrlAsync(string returnUrl, CancellationToken cancellationToken = default)
                => Task.FromResult($"/api/account/external/fake/callback?code=fake-code&state={Uri.EscapeDataString(stateStore.Create(returnUrl))}");
            public Task<ExternalLoginIdentity?> ResolveIdentityAsync(string callbackCode, string state, CancellationToken cancellationToken = default)
                => Task.FromResult<ExternalLoginIdentity?>(callbackCode == "fake-code" ? new ExternalLoginIdentity("fake-user", "fake-user@example.com", "Fake User") : null);
        }

        [Fact]
        public async Task Vendor_mail_adapters_send_requests_without_logging_secrets()
        {
            var handler = new RecordingHandler();
            var clients = new RecordingHttpClientFactory(handler);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mail:SendGrid:ApiKey"] = "sendgrid-secret",
                ["Mail:SendGrid:From"] = "no-reply@example.com",
                ["Mail:Mailgun:ApiKey"] = "mailgun-secret",
                ["Mail:Mailgun:Domain"] = "example.test",
                ["Mail:Mailgun:From"] = "no-reply@example.com"
            }).Build();

            await new SendGridEmailProvider(clients, configuration).SendAsync("person@example.com", "Subject", "Body");
            Assert.Equal("https://api.sendgrid.com/v3/mail/send", handler.LastRequest!.RequestUri!.ToString());
            Assert.Equal("Bearer sendgrid-secret", handler.LastRequest.Headers.Authorization!.ToString());
            await new MailgunEmailProvider(clients, configuration).SendAsync("person@example.com", "Subject", "Body");
            Assert.Equal("https://api.mailgun.net/v3/example.test/messages", handler.LastRequest.RequestUri!.ToString());
            Assert.StartsWith("Basic ", handler.LastRequest.Headers.Authorization!.ToString(), StringComparison.Ordinal);
        }

        private sealed class RecordingHttpClientFactory(RecordingHandler handler) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            public HttpRequestMessage? LastRequest { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
            }
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
        public CapturingEmailProvider EmailProvider { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Audit:Enabled", AuditEnabled.ToString());
            builder.UseSetting("FrontendUrl", "https://localhost");
            builder.UseSetting("DataProtection:KeyDirectory", Path.Combine(Path.GetTempPath(), "dotisan-test-keys"));
            builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
            builder.ConfigureServices(services =>
            {
                connection = new SqliteConnection("Data Source=:memory:");
                connection.Open();
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddSingleton<IEmailProvider>(EmailProvider);
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
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
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

        public async Task<string> SeedMfaUserAsync(string email)
        {
            using var scope = Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await users.CreateAsync(user, "Password1!");
            if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
            await users.SetTwoFactorEnabledAsync(user, true);
            return (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 1))?.Single() ?? throw new InvalidOperationException("Recovery code generation failed.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                connection?.Dispose();
            base.Dispose(disposing);
        }
    }

    public sealed class CapturingEmailProvider : IEmailProvider
    {
        public string LastBody { get; private set; } = string.Empty;

        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
        {
            LastBody = body;
            return Task.CompletedTask;
        }
    }
    """;

    private static string PublicAuthenticationTests() => """
        [Fact]
        public async Task Public_registration_requires_email_confirmation_before_protected_access()
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
            var antiforgery = await GetAntiforgeryToken(client);
            using var register = new HttpRequestMessage(HttpMethod.Post, "/api/account/register");
            register.Headers.Add("X-XSRF-TOKEN", antiforgery);
            register.Content = JsonContent.Create(new { email = "person@example.com", password = "Password1!" });

            var registration = await client.SendAsync(register);
            Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);
        }

        [Fact]
        public async Task Registration_email_contains_a_confirmable_frontend_link()
        {
            var email = $"confirm-{Guid.NewGuid():N}@example.com";
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
            var antiforgery = await GetAntiforgeryToken(client);
            using var register = new HttpRequestMessage(HttpMethod.Post, "/api/account/register");
            register.Headers.Add("X-XSRF-TOKEN", antiforgery);
            register.Content = JsonContent.Create(new { email, password = "Password1!" });

            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(register)).StatusCode);
            var link = factory.EmailProvider.LastBody.Split('\n', StringSplitOptions.RemoveEmptyEntries).Single(line => line.Contains("/auth/confirm-email?", StringComparison.Ordinal)).Trim();
            var query = link[(link.IndexOf('?') + 1)..].Split('&').Select(part => part.Split('=', 2)).ToDictionary(part => part[0], part => Uri.UnescapeDataString(part[1]));
            using var confirmation = new HttpRequestMessage(HttpMethod.Post, "/api/account/email-confirmation/confirm");
            confirmation.Headers.Add("X-XSRF-TOKEN", await GetAntiforgeryToken(client));
            confirmation.Content = JsonContent.Create(new { email = query["email"], token = query["token"] });

            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(confirmation)).StatusCode);
        }

        [Fact]
        public async Task Logout_clears_the_authentication_cookie()
        {
            using var client = await SignInAsync("logout@example.com");

            var antiforgery = await GetAntiforgeryToken(client);
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

    private static string ViteConfig(int apiPort, int webPort) => $$"""
    import { fileURLToPath, URL } from 'node:url';
    import { defineConfig } from 'vite';
    import vue from '@vitejs/plugin-vue';

    export default defineConfig({
      plugins: [vue()],
      resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
      server: { port: {{webPort}}, proxy: { '/api': 'http://localhost:{{apiPort}}' } }
    });
    """;

    private static int DevelopmentApiPort(string name) => 5000 + Math.Abs(name.Aggregate(17, (hash, character) => unchecked(hash * 31 + character))) % 1000;

    private static int DevelopmentWebPort(string name) => 5173 + Math.Abs(name.Aggregate(23, (hash, character) => unchecked(hash * 31 + character))) % 1000;

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
                new("account.login", "Account", "Login", "POST", "/api/account/login", "LoginRequest", "LoginResponse", false, null, null, ["Account"], true, false),
                new("account.logout", "Account", "Logout", "POST", "/api/account/logout", "System.Void", "System.Void", true, "authenticated", null, ["Account"], false, false),
                new("account.me", "Account", "Me", "GET", "/api/account/me", "System.Void", "CurrentUserResponse", true, "authenticated", null, ["Account"], false, false),
                new("account.register", "Account", "Register", "POST", "/api/account/register", "RegisterRequest", "System.Void", false, null, null, ["Account"], true, false),
                new("account.passwordResetRequest", "Account", "RequestPasswordReset", "POST", "/api/account/password-reset/request", "PasswordResetRequest", "System.Void", false, null, null, ["Account"], true, false),
                new("account.passwordResetConfirm", "Account", "ConfirmPasswordReset", "POST", "/api/account/password-reset/confirm", "PasswordResetConfirmRequest", "System.Void", false, null, null, ["Account"], true, false),
                new("account.emailConfirmationConfirm", "Account", "ConfirmEmail", "POST", "/api/account/email-confirmation/confirm", "EmailConfirmationRequest", "System.Void", false, null, null, ["Account"], true, false),
                new("account.emailConfirmationResend", "Account", "ResendConfirmation", "POST", "/api/account/email-confirmation/resend", "EmailConfirmationResendRequest", "System.Void", false, null, null, ["Account"], true, false),
                new("account.mfaSetup", "Account", "GetMfaSetup", "GET", "/api/account/mfa/setup", "System.Void", "MfaSetupResponse", true, "authenticated", null, ["Account"], false, false),
                new("account.mfaVerify", "Account", "VerifyMfa", "POST", "/api/account/mfa/verify", "MfaCodeRequest", "System.Void", true, "authenticated", null, ["Account"], true, false),
                new("account.mfaDisable", "Account", "DisableMfa", "POST", "/api/account/mfa/disable", "System.Void", "System.Void", true, "authenticated", null, ["Account"], false, false),
                new("account.mfaRecoveryCodes", "Account", "RegenerateRecoveryCodes", "POST", "/api/account/mfa/recovery-codes/regenerate", "System.Void", "RecoveryCodesResponse", true, "authenticated", null, ["Account"], false, false),
                new("account.mfaChallenge", "Account", "ChallengeMfa", "POST", "/api/account/mfa/challenge", "MfaCodeRequest", "System.Void", false, null, null, ["Account"], true, false),
                new("account.sessions", "Account", "ListSessions", "GET", "/api/account/sessions", "System.Void", "SessionsResponse", true, "authenticated", null, ["Account"], false, false),
                new("account.revokeSession", "Account", "RevokeSession", "DELETE", "/api/account/sessions/{id}", "System.Void", "System.Void", true, "authenticated", null, ["Account"], true, false),
                new("account.revokeAllSessions", "Account", "RevokeAllSessions", "POST", "/api/account/sessions/revoke-all", "System.Void", "System.Void", true, "authenticated", null, ["Account"], true, false)
                ,new("account.externalProviders", "Account", "ListExternalProviders", "GET", "/api/account/external/providers", "System.Void", "ExternalProvidersResponse", false, null, null, ["Account"], false, false)
                ,new("account.externalChallenge", "Account", "ExternalLoginChallenge", "GET", "/api/account/external/{provider}/challenge", "System.Void", "System.Void", false, null, null, ["Account"], false, false)
                ,new("account.externalCallback", "Account", "ExternalLoginCallback", "GET", "/api/account/external/{provider}/callback", "System.Void", "System.Void", false, null, null, ["Account"], false, false)
                ,new("account.externalLink", "Account", "LinkExternalProvider", "POST", "/api/account/external/{provider}/link", "System.Void", "System.Void", true, "authenticated", null, ["Account"], true, false)
                ,new("account.externalUnlink", "Account", "UnlinkExternalProvider", "DELETE", "/api/account/external/{provider}/link", "System.Void", "System.Void", true, "authenticated", null, ["Account"], true, false)
            ],
            [
                new("AntiforgeryResponse", "AntiforgeryResponse", [new ContractProperty("token", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], []),
                new("CurrentUserResponse", "CurrentUserResponse", [new ContractProperty("id", new ContractTypeDescriptor(ContractTypeKind.String), false, false), new ContractProperty("email", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], []),
                new("LoginRequest", "LoginRequest", [new ContractProperty("email", new ContractTypeDescriptor(ContractTypeKind.String), false, false), new ContractProperty("password", new ContractTypeDescriptor(ContractTypeKind.String), false, false), new ContractProperty("rememberMe", new ContractTypeDescriptor(ContractTypeKind.Boolean), false, false)], []),
                new("LoginResponse", "LoginResponse", [new ContractProperty("code", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], []),
                new("RegisterRequest", "RegisterRequest", [new ContractProperty("email", new ContractTypeDescriptor(ContractTypeKind.String), false, false), new ContractProperty("password", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], [])
                ,new("PasswordResetRequest", "PasswordResetRequest", [new ContractProperty("email", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], [])
                ,new("PasswordResetConfirmRequest", "PasswordResetConfirmRequest", [new ContractProperty("email", new ContractTypeDescriptor(ContractTypeKind.String), false, false), new ContractProperty("token", new ContractTypeDescriptor(ContractTypeKind.String), false, false), new ContractProperty("newPassword", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], [])
                ,new("EmailConfirmationRequest", "EmailConfirmationRequest", [new ContractProperty("email", new ContractTypeDescriptor(ContractTypeKind.String), false, false), new ContractProperty("token", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], [])
                ,new("EmailConfirmationResendRequest", "EmailConfirmationResendRequest", [new ContractProperty("email", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], [])
                ,new("MfaCodeRequest", "MfaCodeRequest", [new ContractProperty("code", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], [])
                ,new("MfaSetupResponse", "MfaSetupResponse", [new ContractProperty("sharedKey", new ContractTypeDescriptor(ContractTypeKind.String), false, false), new ContractProperty("authenticatorUri", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], [])
                ,new("RecoveryCodesResponse", "RecoveryCodesResponse", [new ContractProperty("recoveryCodes", new ContractTypeDescriptor(ContractTypeKind.Array, null, new ContractTypeDescriptor(ContractTypeKind.String)), false, false)], [])
                ,new("SessionInfo", "SessionInfo", [new ContractProperty("id", new ContractTypeDescriptor(ContractTypeKind.Guid), false, false), new ContractProperty("deviceName", new ContractTypeDescriptor(ContractTypeKind.String), false, false), new ContractProperty("createdAt", new ContractTypeDescriptor(ContractTypeKind.DateTime), false, false), new ContractProperty("lastSeenAt", new ContractTypeDescriptor(ContractTypeKind.DateTime), false, false), new ContractProperty("expiresAt", new ContractTypeDescriptor(ContractTypeKind.DateTime), false, false), new ContractProperty("userAgent", new ContractTypeDescriptor(ContractTypeKind.String), true, false), new ContractProperty("ipAddress", new ContractTypeDescriptor(ContractTypeKind.String), true, false)], [])
                ,new("SessionsResponse", "SessionsResponse", [new ContractProperty("sessions", new ContractTypeDescriptor(ContractTypeKind.Array, null, new ContractTypeDescriptor(ContractTypeKind.Object, "SessionInfo")), false, false)], [])
                ,new("ExternalLoginProviderDescriptor", "ExternalLoginProviderDescriptor", [new ContractProperty("name", new ContractTypeDescriptor(ContractTypeKind.String), false, false), new ContractProperty("displayName", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], [])
                ,new("ExternalProvidersResponse", "ExternalProvidersResponse", [new ContractProperty("providers", new ContractTypeDescriptor(ContractTypeKind.Array, null, new ContractTypeDescriptor(ContractTypeKind.Object, "ExternalLoginProviderDescriptor")), false, false)], [])
            ],
            [
                EndpointContractMetadata.Create("GET", "/api/account/antiforgery", null, [], ["Account"], false),
                EndpointContractMetadata.Create("POST", "/api/account/login", new EndpointRequestBodyMetadata(new ContractTypeDescriptor(ContractTypeKind.Object, "LoginRequest")), [], ["Account"], true),
                EndpointContractMetadata.Create("POST", "/api/account/logout", null, [], ["Account"], false),
                EndpointContractMetadata.Create("GET", "/api/account/me", null, [], ["Account"], false),
                EndpointContractMetadata.Create("POST", "/api/account/register", new EndpointRequestBodyMetadata(new ContractTypeDescriptor(ContractTypeKind.Object, "RegisterRequest")), [], ["Account"], true)
                ,EndpointContractMetadata.Create("POST", "/api/account/password-reset/request", new EndpointRequestBodyMetadata(new ContractTypeDescriptor(ContractTypeKind.Object, "PasswordResetRequest")), [], ["Account"], true)
                ,EndpointContractMetadata.Create("POST", "/api/account/password-reset/confirm", new EndpointRequestBodyMetadata(new ContractTypeDescriptor(ContractTypeKind.Object, "PasswordResetConfirmRequest")), [], ["Account"], true)
                ,EndpointContractMetadata.Create("POST", "/api/account/email-confirmation/confirm", new EndpointRequestBodyMetadata(new ContractTypeDescriptor(ContractTypeKind.Object, "EmailConfirmationRequest")), [], ["Account"], true)
                ,EndpointContractMetadata.Create("POST", "/api/account/email-confirmation/resend", new EndpointRequestBodyMetadata(new ContractTypeDescriptor(ContractTypeKind.Object, "EmailConfirmationResendRequest")), [], ["Account"], true)
                ,EndpointContractMetadata.Create("GET", "/api/account/mfa/setup", null, [], ["Account"], false)
                ,EndpointContractMetadata.Create("POST", "/api/account/mfa/verify", new EndpointRequestBodyMetadata(new ContractTypeDescriptor(ContractTypeKind.Object, "MfaCodeRequest")), [], ["Account"], true)
                ,EndpointContractMetadata.Create("POST", "/api/account/mfa/disable", null, [], ["Account"], false)
                ,EndpointContractMetadata.Create("POST", "/api/account/mfa/recovery-codes/regenerate", null, [], ["Account"], false)
                ,EndpointContractMetadata.Create("POST", "/api/account/mfa/challenge", new EndpointRequestBodyMetadata(new ContractTypeDescriptor(ContractTypeKind.Object, "MfaCodeRequest")), [], ["Account"], true)
                ,EndpointContractMetadata.Create("GET", "/api/account/sessions", null, [], ["Account"], false)
                ,EndpointContractMetadata.Create("DELETE", "/api/account/sessions/{id}", null, [new EndpointParameterMetadata("id", new ContractTypeDescriptor(ContractTypeKind.Guid), false, false)], ["Account"], true)
                ,EndpointContractMetadata.Create("POST", "/api/account/sessions/revoke-all", null, [], ["Account"], true)
                ,EndpointContractMetadata.Create("GET", "/api/account/external/providers", null, [], ["Account"], false)
                ,EndpointContractMetadata.Create("GET", "/api/account/external/{provider}/challenge", null, [new EndpointParameterMetadata("provider", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], ["Account"], false)
                ,EndpointContractMetadata.Create("GET", "/api/account/external/{provider}/callback", null, [new EndpointParameterMetadata("provider", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], ["Account"], false)
                ,EndpointContractMetadata.Create("POST", "/api/account/external/{provider}/link", null, [new EndpointParameterMetadata("provider", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], ["Account"], true)
                ,EndpointContractMetadata.Create("DELETE", "/api/account/external/{provider}/link", null, [new EndpointParameterMetadata("provider", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], ["Account"], true)
            ])
        : new ContractManifest(1, [], []);

    private static string RoutesIndex(bool authenticationEnabled, bool notificationsEnabled, bool importsExportsEnabled, bool webhooksEnabled) => authenticationEnabled
        ? $$"""
    import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router';
    import { me } from '../dotisan/services';
    import PortalLayout from '../layouts/PortalLayout.vue';
    import AuthLayout from '../layouts/AuthLayout.vue';
    import DashboardPage from '../pages/DashboardPage.vue';
    import LoginPage from '../pages/auth/LoginPage.vue';
    import RegisterPage from '../pages/auth/RegisterPage.vue';
    import ForgotPasswordPage from '../pages/auth/ForgotPasswordPage.vue';
    import EmailConfirmationPage from '../pages/auth/EmailConfirmationPage.vue';
    import MfaChallengePage from '../pages/auth/MfaChallengePage.vue';
    import ProfilePage from '../pages/account/ProfilePage.vue';
    import MfaPage from '../pages/account/MfaPage.vue';
    import SessionsPage from '../pages/account/SessionsPage.vue';
    import ExternalLoginsPage from '../pages/account/ExternalLoginsPage.vue';
    import AuthorizationPage from '../pages/admin/AuthorizationPage.vue';
    {{(notificationsEnabled ? "import NotificationsPage from '../pages/NotificationsPage.vue';" : string.Empty)}}
    {{(importsExportsEnabled ? "import ImportsExportsPage from '../pages/ImportsExportsPage.vue';" : string.Empty)}}
    {{(webhooksEnabled ? "import WebhooksPage from '../pages/WebhooksPage.vue';" : string.Empty)}}

    const routes: RouteRecordRaw[] = [
      { path: '/', component: PortalLayout, meta: { requiresAuth: true }, children: [{ path: '', component: DashboardPage }, {{(notificationsEnabled ? "{ path: 'notifications', component: NotificationsPage }," : string.Empty)}} {{(importsExportsEnabled ? "{ path: 'data', component: ImportsExportsPage }," : string.Empty)}} {{(webhooksEnabled ? "{ path: 'webhooks', component: WebhooksPage }," : string.Empty)}} { path: 'profile', component: ProfilePage }, { path: 'security/mfa', component: MfaPage }, { path: 'security/sessions', component: SessionsPage }, { path: 'security/external-logins', component: ExternalLoginsPage }, { path: 'admin/authorization', component: AuthorizationPage, meta: { requiresPermission: 'authorization.manage' } }] },
      { path: '/auth', component: AuthLayout, children: [{ path: 'login', name: 'login', component: LoginPage }, { path: 'register', name: 'register', component: RegisterPage }, { path: 'forgot-password', component: ForgotPasswordPage }, { path: 'confirm-email', component: EmailConfirmationPage }, { path: 'mfa-challenge', component: MfaChallengePage }] }
    ];
    // DOTISAN:ROUTES
    const router = createRouter({ history: createWebHistory(), routes });
    router.beforeEach(async (to) => {
      if (!to.meta.requiresAuth && !to.meta.requiresPermission) return true;
      try { await me(); return true; } catch { return { name: 'login', query: { redirect: to.fullPath } }; }
    });
    export default router;
    """
    : $$"""
    import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router';
    import PortalLayout from '../layouts/PortalLayout.vue';
    import DashboardPage from '../pages/DashboardPage.vue';
    {{(notificationsEnabled ? "import NotificationsPage from '../pages/NotificationsPage.vue';" : string.Empty)}}
    {{(importsExportsEnabled ? "import ImportsExportsPage from '../pages/ImportsExportsPage.vue';" : string.Empty)}}
    {{(webhooksEnabled ? "import WebhooksPage from '../pages/WebhooksPage.vue';" : string.Empty)}}

    const routes: RouteRecordRaw[] = [
      { path: '/', component: PortalLayout, children: [{ path: '', component: DashboardPage }{{(notificationsEnabled ? ", { path: 'notifications', component: NotificationsPage }" : string.Empty)}}{{(importsExportsEnabled ? ", { path: 'data', component: ImportsExportsPage }" : string.Empty)}}{{(webhooksEnabled ? ", { path: 'webhooks', component: WebhooksPage }" : string.Empty)}}] }
    ];
    // DOTISAN:ROUTES
    export default createRouter({ history: createWebHistory(), routes });
    """;

    private static string ApiClient() => """
    export async function issueAntiforgery(): Promise<string> {
      const response = await fetch('/api/account/antiforgery', { credentials: 'include' });
      if (!response.ok) throw new Error('Could not obtain an antiforgery token.');
      return (await response.json() as { token: string }).token;
    }

    export async function request<T>(url: string, init: RequestInit = {}): Promise<T> {
      const method = (init.method ?? 'GET').toUpperCase();
      const headers = new Headers(init.headers);
      if (method !== 'GET' && method !== 'HEAD' && method !== 'OPTIONS') headers.set('X-XSRF-TOKEN', await issueAntiforgery());
      const response = await fetch(url, { ...init, headers, credentials: 'include' });
      if (!response.ok) throw new Error(`Request failed (${response.status}).`);
      return response.status === 204 ? undefined as T : await response.json() as T;
    }
    """;

    private static string JobTests(string identifier) => $$"""
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Data.Sqlite;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using JasperFx.Resources;
    using Wolverine;
    using {{identifier}}.Api.Jobs;

    namespace {{identifier}}.Api.Tests;

    public sealed class JobTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> factory;

        public JobTests(WebApplicationFactory<Program> factory)
        {
            this.factory = factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("Logging:EventLog:LogLevel:Default", "None");
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Dotisan:Jobs:Enabled"] = "false",
                    ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:"
                }));
            });
        }

        [Fact]
        public async Task Sample_job_endpoint_dispatches_through_wolverine()
        {
            SampleJobHandler.ResetTestCounters();
            var bus = factory.Services.GetRequiredService<IMessageBus>();
            await bus.SendAsync(new SampleJob(DateTimeOffset.UtcNow));
            for (var attempt = 0; attempt < 50 && SampleJobHandler.ProcessedCount == 0; attempt++)
                await Task.Delay(100);
            Assert.True(SampleJobHandler.ProcessedCount > 0);
            using var client = factory.CreateClient();

            using var response = await client.PostAsync("/api/jobs/sample", content: null);

            Assert.Equal(System.Net.HttpStatusCode.Accepted, response.StatusCode);
        }

        [Fact]
        public async Task Sample_job_can_be_scheduled_with_the_durable_store()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"dotisan-v06-{Guid.NewGuid():N}.db");
            WebApplicationFactory<Program>? durableFactory = null;
            try
            {
                durableFactory = CreateDurableFactory(databasePath);
                using var client = durableFactory.CreateClient();
                await durableFactory.Services.GetRequiredService<IHost>().SetupResources();
                var bus = durableFactory.Services.GetRequiredService<IMessageBus>();

                await bus.ScheduleAsync(new SampleJob(DateTimeOffset.UtcNow), TimeSpan.FromMinutes(30));
                durableFactory.Dispose();
                durableFactory = CreateDurableFactory(databasePath);
                using var restartedClient = durableFactory.CreateClient();
                await durableFactory.Services.GetRequiredService<IHost>().SetupResources();

                using var connection = new SqliteConnection($"Data Source={databasePath}");
                await connection.OpenAsync();
                await using var tablesCommand = connection.CreateCommand();
                tablesCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name LIKE 'wolverine%'";
                await using var tablesReader = await tablesCommand.ExecuteReaderAsync();
                var tableNames = new List<string>();
                while (await tablesReader.ReadAsync())
                    tableNames.Add(tablesReader.GetString(0));
                Assert.NotEmpty(tableNames);

                var persistedCount = 0L;
                foreach (var tableName in tableNames)
                {
                    await using var countCommand = connection.CreateCommand();
                    countCommand.CommandText = $"SELECT COUNT(*) FROM \"{tableName.Replace("\"", "\"\"")}\"";
                    persistedCount += (long)(await countCommand.ExecuteScalarAsync() ?? 0L);
                }
                Assert.True(persistedCount > 0);
            }
            finally
            {
                durableFactory?.Dispose();
                if (File.Exists(databasePath))
                    await DeleteDatabaseAsync(databasePath);
            }
        }

        [Fact]
        public async Task Sample_job_retries_until_the_configured_failure_clears()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"dotisan-v06-retry-{Guid.NewGuid():N}.db");
            SampleJobHandler.ResetTestCounters();
            SampleJobHandler.FailuresBeforeSuccess = 2;
            WebApplicationFactory<Program>? durableFactory = null;
            try
            {
                durableFactory = CreateDurableFactory(databasePath);
                using var client = durableFactory.CreateClient();
                var bus = durableFactory.Services.GetRequiredService<IMessageBus>();
                await bus.SendAsync(new SampleJob(DateTimeOffset.UtcNow));

                for (var attempt = 0; attempt < 100 && SampleJobHandler.ProcessedCount == 0; attempt++)
                    await Task.Delay(100);

                Assert.Equal(3, SampleJobHandler.AttemptCount);
                Assert.Equal(1, SampleJobHandler.ProcessedCount);
            }
            finally
            {
                durableFactory?.Dispose();
                SampleJobHandler.FailuresBeforeSuccess = 0;
                if (File.Exists(databasePath))
                    await DeleteDatabaseAsync(databasePath);
            }
        }

        private static async Task DeleteDatabaseAsync(string databasePath)
        {
            for (var attempt = 0; attempt < 20 && File.Exists(databasePath); attempt++)
            {
                try
                {
                    File.Delete(databasePath);
                }
                catch (IOException)
                {
                    await Task.Delay(100);
                }
            }
        }

        private WebApplicationFactory<Program> CreateDurableFactory(string databasePath) => factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Dotisan:Jobs:Enabled", "true");
            builder.UseSetting("Dotisan:Jobs:RetryDelaySeconds", "1");
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={databasePath}");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Dotisan:Jobs:Enabled"] = "true",
                ["Dotisan:Jobs:RetryDelaySeconds"] = "1",
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={databasePath}"
            }));
            builder.ConfigureServices(services => services.AddResourceSetupOnStartup());
        });

        [Fact]
        public void Generated_job_test_mentions_durable_scheduling_contract()
        {
            Assert.Contains("IMessageBus", typeof(Wolverine.IMessageBus).FullName);
            Assert.Contains("scheduled", "Durable scheduled messages survive process restarts", StringComparison.OrdinalIgnoreCase);
        }
    }

    """;

    private static string TenantContextTests(string identifier) => $$"""
    using System.Security.Claims;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.Hosting;
    using {{identifier}}.Api.Tenancy;

    namespace {{identifier}}.Api.Tests;

    public sealed class TenantContextTests
    {
        [Fact]
        public void Authenticated_claim_takes_precedence_over_development_header()
        {
            var context = new DefaultHttpContext();
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenant_id", "tenant-a")]));
            context.Request.Headers["X-Tenant-ID"] = "tenant-b";
            var tenant = new TenantContext(new HttpContextAccessor { HttpContext = context }, Environment("Development"));

            Assert.Equal("tenant-a", tenant.TenantId);
        }

        [Fact]
        public void Development_header_is_a_supported_fallback()
        {
            var context = new DefaultHttpContext();
            context.Request.Headers["X-Tenant-ID"] = " tenant-b ";
            var tenant = new TenantContext(new HttpContextAccessor { HttpContext = context }, Environment("Development"));

            Assert.Equal("tenant-b", tenant.RequireTenantId());
        }

        [Fact]
        public void Production_rejects_header_only_tenant_identity()
        {
            var context = new DefaultHttpContext();
            context.Request.Headers["X-Tenant-ID"] = "tenant-b";
            var tenant = new TenantContext(new HttpContextAccessor { HttpContext = context }, Environment("Production"));

            Assert.Throws<InvalidOperationException>(() => tenant.TenantId);
        }

        private static TestEnvironment Environment(string name) => new() { EnvironmentName = name };

        private sealed class TestEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = Environments.Development;
            public string ApplicationName { get; set; } = "TenantTests";
            public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
    """;

    private static string AppVue() => """
    <template><RouterView /></template>
    """;

    private static string PlaywrightConfig() => """
    import { defineConfig, devices } from '@playwright/test';

    export default defineConfig({
      testDir: './tests/e2e',
      reporter: 'list',
      use: { baseURL: 'http://127.0.0.1:4173' },
      webServer: { command: 'pnpm dev --host 127.0.0.1 --port 4173', port: 4173, reuseExistingServer: !process.env.CI },
      projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }]
    });
    """;

    private static string VitestConfig() => """
    import { defineConfig } from 'vitest/config';
    import vue from '@vitejs/plugin-vue';

    export default defineConfig({
      plugins: [vue()],
      test: { environment: 'jsdom', include: ['src/**/*.test.ts'], exclude: ['tests/e2e/**'] }
    });
    """;

    private static string TailwindConfig() => """
    import type { Config } from 'tailwindcss';

    export default {
      content: ['./index.html', './src/**/*.{vue,js,ts,jsx,tsx}'],
      theme: { extend: {} },
      plugins: []
    } satisfies Config;
    """;

    private static string PostCssConfig() => """
    module.exports = { plugins: { tailwindcss: {}, autoprefixer: {} } };
    """;

    private static string ShadcnComponentsConfig() => """
    {
      "$schema": "https://ui.shadcn.com/schema.json",
      "style": "default",
      "tailwind": { "config": "tailwind.config.ts", "css": "src/style.css", "baseColor": "slate", "cssVariables": true },
      "aliases": { "components": "@/components", "utils": "@/lib/utils", "ui": "@/components/ui" }
    }
    """;

    private static string UiBadgeTest() => """
    import { mount } from '@vue/test-utils';
    import { describe, expect, it } from 'vitest';
    import UiBadge from './Badge.vue';

    describe('UiBadge', () => {
      it('renders its slot content', () => {
        const wrapper = mount(UiBadge, { slots: { default: 'Ready' } });
        expect(wrapper.text()).toBe('Ready');
      });
    });
    """;

    private static string ShadcnUtils() => """
    import { type ClassValue, clsx } from 'clsx';
    import { twMerge } from 'tailwind-merge';

    export function cn(...inputs: ClassValue[]) {
      return twMerge(clsx(inputs));
    }
    """;

    private static string FrontendSmokeTest() => """
    import { expect, test } from '@playwright/test';

    test('renders the Dotisan application shell', async ({ page }) => {
      await page.goto('/');
      await expect(page.locator('body')).toContainText('Dotisan');
    });
    """;

    private static string UiButton() => """
    <script setup lang="ts">
    withDefaults(defineProps<{ variant?: 'default' | 'outline' | 'ghost' | 'destructive'; type?: 'button' | 'submit' | 'reset'; disabled?: boolean }>(), { variant: 'default', type: 'button' });
    </script>
    <template><button :type="type" :disabled="disabled" class="ui-button" :class="`ui-button--${variant}`"><slot /></button></template>
    """;

    private static string UiInput() => """
    <script setup lang="ts">
    defineProps<{ modelValue?: string; type?: string; placeholder?: string; autocomplete?: string; required?: boolean; disabled?: boolean }>();
    const emit = defineEmits<{ 'update:modelValue': [value: string] }>();
    </script>
    <template><input :value="modelValue" :type="type ?? 'text'" :placeholder="placeholder" :autocomplete="autocomplete" :required="required" :disabled="disabled" class="ui-input" @input="emit('update:modelValue', ($event.target as HTMLInputElement).value)" /></template>
    """;

    private static string UiCard() => """
    <template><section class="ui-card"><div v-if="$slots.header" class="ui-card__header"><slot name="header" /></div><div class="ui-card__content"><slot /></div><div v-if="$slots.footer" class="ui-card__footer"><slot name="footer" /></div></section></template>
    """;

    private static string UiBadge() => """
    <template><span class="ui-badge"><slot /></span></template>
    """;

    private static string AppSidebar(string name, bool authenticationEnabled) => $$"""
    <script setup lang="ts">
    defineProps<{ open?: boolean }>();
    const links = [
      { label: 'Dashboard', to: '/' },
      {{(authenticationEnabled ? "{ label: 'Profile', to: '/profile' }, { label: 'MFA security', to: '/security/mfa' }, { label: 'Sessions', to: '/security/sessions' }, { label: 'External logins', to: '/security/external-logins' }, { label: 'Authorization', to: '/admin/authorization' }," : string.Empty)}}
    ];
    </script>
    <template>
      <aside class="app-sidebar" :class="{ 'app-sidebar--open': open }" aria-label="Primary navigation">
        <RouterLink to="/" class="brand"><span class="brand-mark">D</span><span>{{name}}</span></RouterLink>
        <nav><RouterLink v-for="link in links" :key="link.to" :to="link.to" class="nav-link" active-class="nav-link--active" v-text="link.label"></RouterLink></nav>
        <div class="sidebar-footer"><span class="sidebar-caption">Built with Dotisan</span></div>
      </aside>
    </template>
    """;

    private static string AppHeader(string name, bool authenticationEnabled) => $$"""
    <script setup lang="ts">
    import { ref } from 'vue';
    import UiButton from './ui/Button.vue';
    {{(authenticationEnabled ? "import { logout } from '../dotisan/services';" : string.Empty)}}
    const menuOpen = ref(false);
    {{(authenticationEnabled ? "async function signOut() { await logout(); window.location.assign('/auth/login'); }" : string.Empty)}}
    </script>
    <template>
      <header class="app-header">
        <div><p class="header-kicker">Workspace</p><h1>{{name}}</h1></div>
        {{(authenticationEnabled ? "<div class=\"user-menu\"><UiButton variant=\"ghost\" @click=\"menuOpen = !menuOpen\">Account ▾</UiButton><div v-if=\"menuOpen\" class=\"user-menu__panel\"><RouterLink to=\"/profile\">Profile</RouterLink><UiButton variant=\"ghost\" @click=\"signOut\">Sign out</UiButton></div></div>" : string.Empty)}}
      </header>
    </template>
    """;

    private static string PortalLayout() => """
    <script setup lang="ts">
    import AppSidebar from '../components/AppSidebar.vue';
    import AppHeader from '../components/AppHeader.vue';
    </script>
    <template><div class="portal-layout"><AppSidebar /><div class="portal-main"><AppHeader /><main class="portal-content"><RouterView /></main></div></div></template>
    """;

    private static string AuthLayout() => """
    <template><main class="auth-layout"><div class="auth-brand"><span class="brand-mark">D</span><span>Dotisan</span></div><RouterView /></main></template>
    """;

    private static string DashboardPage(string name) => $$"""
    <script setup lang="ts">
    import UiBadge from '../components/ui/Badge.vue';
    import UiCard from '../components/ui/Card.vue';
    </script>
    <template>
      <section class="page-stack">
        <div class="page-heading"><div><p class="eyebrow">Overview</p><h2>Welcome to {{name}}</h2><p class="page-lede">Your application workspace is ready. Start by adding your first feature.</p></div><UiBadge>Ready</UiBadge></div>
        <div class="dashboard-grid"><UiCard><template #header><h3>Application status</h3></template><p class="metric">Online</p><p class="muted">The API and Vue portal are connected.</p></UiCard><UiCard><template #header><h3>Next step</h3></template><p class="metric">Build a feature</p><p class="muted">Use <code>dotisan make:resource</code> to create your first vertical slice.</p></UiCard></div>
      </section>
    </template>
    """;

    private static string NotificationsPage() => """
    <script setup lang="ts">
    import { onMounted, ref } from 'vue';
    import UiCard from '../components/ui/Card.vue';
    import { issueAntiforgery } from '../dotisan/services';
    const notifications = ref<Array<{ id: string; title: string; body: string; isRead: boolean }>>([]); const pending = ref<string | null>(null); const message = ref('');
    onMounted(async () => { const response = await fetch('/api/notifications', { credentials: 'include' }); if (response.ok) notifications.value = (await response.json()).notifications; });
    async function markRead(id: string) { if (pending.value) return; pending.value = id; message.value = ''; try { const token = await issueAntiforgery(); const response = await fetch(`/api/notifications/${id}/read`, { method: 'POST', credentials: 'include', headers: { 'X-XSRF-TOKEN': token.token } }); if (!response.ok) throw new Error(); const item = notifications.value.find(value => value.id === id); if (item) item.isRead = true; } catch { message.value = 'Could not mark the notification as read.'; } finally { pending.value = null; } }
    </script>
    <template><section class="page-stack"><div class="page-heading"><div><p class="eyebrow">Inbox</p><h2>Notifications</h2></div></div><p v-if="message" role="alert" v-text="message"></p><UiCard v-for="item in notifications" :key="item.id" :class="{ unread: !item.isRead }"><h3>{{ item.title }}</h3><p>{{ item.body }}</p><button v-if="!item.isRead" :disabled="pending !== null" @click="markRead(item.id)">{{ pending === item.id ? 'Saving...' : 'Mark read' }}</button></UiCard><p v-if="notifications.length === 0" class="muted">You are all caught up.</p></section></template>
    """;

    private static string ImportsExportsPage() => """
    <script setup lang="ts">
    import { ref } from 'vue';
    import { issueAntiforgery } from '../dotisan/services';
    const file = ref<File>(); const message = ref(''); const pending = ref(false);
    async function importFile() { if (!file.value || pending.value) return; pending.value = true; message.value = ''; try { const body = new FormData(); body.append('file', file.value); const token = await issueAntiforgery(); const response = await fetch('/api/data/imports', { method: 'POST', body, credentials: 'include', headers: { 'X-XSRF-TOKEN': token.token } }); message.value = response.ok ? 'Import queued.' : 'Import failed.'; } catch { message.value = 'Import failed.'; } finally { pending.value = false; } }
    </script>
    <template><section class="page-stack"><div class="page-heading"><div><p class="eyebrow">Data</p><h2>Imports and exports</h2></div></div><form @submit.prevent="importFile"><input type="file" accept=".csv,.json" @change="file = ($event.target as HTMLInputElement).files?.[0]" /><button type="submit" :disabled="pending">{{ pending ? 'Queueing...' : 'Queue import' }}</button></form><a href="/api/data/exports/csv">Download CSV export</a><p v-if="message" role="status">{{ message }}</p></section></template>
    """;

    private static string WebhooksPage() => """
    <script setup lang="ts">
    import { onMounted, ref } from 'vue';
    import { issueAntiforgery } from '../dotisan/services';
    const deliveries = ref<Array<{ id: string; eventType: string; endpoint: string; status: string; attempts: number }>>([]); const pending = ref<string | null>(null);
    onMounted(async () => { const response = await fetch('/api/webhooks/deliveries'); if (response.ok) deliveries.value = await response.json(); });
    async function replay(id: string) { if (pending.value) return; pending.value = id; try { const token = await issueAntiforgery(); await fetch(`/api/webhooks/deliveries/${id}/replay`, { method: 'POST', credentials: 'include', headers: { 'X-XSRF-TOKEN': token.token } }); } finally { pending.value = null; } }
    </script>
    <template><section class="page-stack"><div class="page-heading"><div><p class="eyebrow">Integrations</p><h2>Webhook deliveries</h2></div></div><table><caption class="sr-only">Webhook deliveries</caption><thead><tr><th scope="col">Event</th><th scope="col">Endpoint</th><th scope="col">Status</th><th scope="col">Attempts</th><th scope="col">Actions</th></tr></thead><tbody><tr v-for="delivery in deliveries" :key="delivery.id"><td>{{ delivery.eventType }}</td><td>{{ delivery.endpoint }}</td><td>{{ delivery.status }}</td><td>{{ delivery.attempts }}</td><td><button :disabled="pending !== null" @click="replay(delivery.id)">{{ pending === delivery.id ? 'Replaying...' : 'Replay' }}</button></td></tr></tbody></table><p v-if="deliveries.length === 0" class="muted">No webhook deliveries yet.</p></section></template>
    """;

    private static string LoginPage() => """
    <script setup lang="ts">
    import { ref } from 'vue'; import { useRoute, useRouter } from 'vue-router'; import UiButton from '../../components/ui/Button.vue'; import UiCard from '../../components/ui/Card.vue'; import UiInput from '../../components/ui/Input.vue'; import { ApiError, issueAntiforgery, login } from '../../dotisan/services';
    const router = useRouter(); const route = useRoute(); const email = ref(''); const password = ref(''); const rememberMe = ref(false); const error = ref(''); const fieldErrors = ref<Record<string, string[]>>({}); const pending = ref(false);
    function clearErrors() { error.value = ''; fieldErrors.value = {}; }
    async function submit() { pending.value = true; clearErrors(); try { const token = await issueAntiforgery(); const result = await login({ email: email.value, password: password.value, rememberMe: rememberMe.value }, { headers: { 'X-XSRF-TOKEN': token.token } }); if (result.code === 'mfa_required') { await router.push({ path: '/auth/mfa-challenge', query: { redirect: String(route.query.redirect ?? '/') } }); return; } await router.push(String(route.query.redirect ?? '/')); } catch (exception) { if (exception instanceof ApiError) { error.value = exception.message; fieldErrors.value = exception.fieldErrors; } else { error.value = exception instanceof Error ? exception.message : 'Unable to sign in.'; } } finally { pending.value = false; } }
    </script>
    <template>
    <UiCard><div class="auth-card-heading"><p class="eyebrow">Welcome back</p><h1>Sign in</h1><p class="muted">Continue to your workspace.</p></div><form class="form-stack" @submit.prevent="submit"><p v-if="error" class="form-error" role="alert" v-text="error"></p><label for="login-email">Email<UiInput id="login-email" v-model="email" type="email" autocomplete="email" required :aria-invalid="fieldErrors.email?.length ? 'true' : undefined" aria-describedby="login-email-error" /></label><p v-if="fieldErrors.email?.length" id="login-email-error" class="field-error" v-text="fieldErrors.email.join(' ')"></p><label for="login-password">Password<UiInput id="login-password" v-model="password" type="password" autocomplete="current-password" required :aria-invalid="fieldErrors.password?.length ? 'true' : undefined" aria-describedby="login-password-error" /></label><p v-if="fieldErrors.password?.length" id="login-password-error" class="field-error" v-text="fieldErrors.password.join(' ')"></p><label class="checkbox"><input v-model="rememberMe" type="checkbox" /> Remember me</label><UiButton type="submit" :disabled="pending"><span v-text="pending ? 'Signing in...' : 'Sign in'"></span></UiButton></form><p class="form-links"><RouterLink to="/auth/register">Create an account</RouterLink><RouterLink to="/auth/forgot-password">Forgot password?</RouterLink></p></UiCard>
    </template>
    """;

    private static string MfaChallengePage() => """
    <script setup lang="ts">
    import { ref } from 'vue'; import { useRoute, useRouter } from 'vue-router'; import UiButton from '../../components/ui/Button.vue'; import UiCard from '../../components/ui/Card.vue'; import UiInput from '../../components/ui/Input.vue'; import { issueAntiforgery, challengeMfa } from '../../dotisan/services';
    const router = useRouter(); const route = useRoute(); const code = ref(''); const error = ref(''); const pending = ref(false);
    async function submit() { pending.value = true; error.value = ''; try { const token = await issueAntiforgery(); await challengeMfa({ code: code.value }, { headers: { 'X-XSRF-TOKEN': token.token } }); await router.push(String(route.query.redirect ?? '/')); } catch (exception) { error.value = exception instanceof Error ? exception.message : 'That MFA code is invalid.'; } finally { pending.value = false; } }
    </script>
    <template><UiCard><div class="auth-card-heading"><p class="eyebrow">Account security</p><h1>Verify your identity</h1><p class="muted">Enter the six-digit code from your authenticator app.</p></div><form class="form-stack" @submit.prevent="submit"><p v-if="error" class="form-error" role="alert" v-text="error"></p><label>Authenticator code<UiInput v-model="code" inputmode="numeric" autocomplete="one-time-code" required /></label><UiButton type="submit" :disabled="pending"><span v-text="pending ? 'Verifying...' : 'Verify code'"></span></UiButton></form><p class="form-links"><RouterLink to="/auth/login">Return to sign in</RouterLink></p></UiCard></template>
    """;

    private static string RegisterPage() => """
    <script setup lang="ts">
    import { ref } from 'vue'; import { useRouter } from 'vue-router'; import UiButton from '../../components/ui/Button.vue'; import UiCard from '../../components/ui/Card.vue'; import UiInput from '../../components/ui/Input.vue'; import { ApiError, issueAntiforgery, register } from '../../dotisan/services';
    const router = useRouter(); const email = ref(''); const password = ref(''); const error = ref(''); const fieldErrors = ref<Record<string, string[]>>({}); const pending = ref(false);
    function clearErrors() { error.value = ''; fieldErrors.value = {}; }
    async function submit() { pending.value = true; clearErrors(); try { const token = await issueAntiforgery(); await register({ email: email.value, password: password.value }, { headers: { 'X-XSRF-TOKEN': token.token } }); await router.push({ path: '/auth/confirm-email', query: { email: email.value } }); } catch (exception) { if (exception instanceof ApiError) { error.value = exception.message; fieldErrors.value = exception.fieldErrors; } else { error.value = exception instanceof Error ? exception.message : 'Unable to create your account.'; } } finally { pending.value = false; } }
    </script>
    <template>
    <UiCard><div class="auth-card-heading"><p class="eyebrow">Get started</p><h1>Create account</h1><p class="muted">Set up your workspace access.</p></div><form class="form-stack" @submit.prevent="submit"><p v-if="error" class="form-error" role="alert" v-text="error"></p><label for="register-email">Email<UiInput id="register-email" v-model="email" type="email" autocomplete="email" required :aria-invalid="fieldErrors.email?.length ? 'true' : undefined" aria-describedby="register-email-error" /></label><p v-if="fieldErrors.email?.length" id="register-email-error" class="field-error" v-text="fieldErrors.email.join(' ')"></p><label for="register-password">Password<UiInput id="register-password" v-model="password" type="password" autocomplete="new-password" required :aria-invalid="fieldErrors.password?.length ? 'true' : undefined" aria-describedby="register-password-error" /></label><p v-if="fieldErrors.password?.length" id="register-password-error" class="field-error" v-text="fieldErrors.password.join(' ')"></p><UiButton type="submit" :disabled="pending"><span v-text="pending ? 'Creating...' : 'Create account'"></span></UiButton></form><p class="form-links"><RouterLink to="/auth/login">Already have an account?</RouterLink></p></UiCard>
    </template>
    """;

    private static string ForgotPasswordPage() => """
    <script setup lang="ts">
    import { ref } from 'vue'; import UiButton from '../../components/ui/Button.vue'; import UiCard from '../../components/ui/Card.vue'; import UiInput from '../../components/ui/Input.vue'; import { issueAntiforgery, requestPasswordReset } from '../../dotisan/services';
    const email = ref(''); const sent = ref(false); const error = ref(''); const pending = ref(false);
    async function submit() { pending.value = true; error.value = ''; try { const token = await issueAntiforgery(); await requestPasswordReset({ email: email.value }, { headers: { 'X-XSRF-TOKEN': token.token } }); sent.value = true; } catch (exception) { error.value = exception instanceof Error ? exception.message : 'Unable to request a reset.'; } finally { pending.value = false; } }
    </script>
    <template>
    <UiCard><div class="auth-card-heading"><p class="eyebrow">Account recovery</p><h1>Forgot password?</h1><p v-if="sent" class="muted">If the account exists, a reset email has been sent.</p><form v-else class="form-stack" @submit.prevent="submit"><p v-if="error" class="form-error" role="alert" v-text="error"></p><label>Email<UiInput v-model="email" type="email" autocomplete="email" required /></label><UiButton type="submit" :disabled="pending"><span v-text="pending ? 'Sending...' : 'Send reset email'"></span></UiButton></form></div><p class="form-links"><RouterLink to="/auth/login">Return to sign in</RouterLink></p></UiCard>
    </template>
    """;

    private static string EmailConfirmationPage() => """
    <script setup lang="ts">
    import { onMounted, ref } from 'vue'; import { useRoute } from 'vue-router'; import UiButton from '../../components/ui/Button.vue'; import UiCard from '../../components/ui/Card.vue'; import UiInput from '../../components/ui/Input.vue'; import { confirmEmail, issueAntiforgery, resendConfirmation } from '../../dotisan/services';
    const route = useRoute(); const email = ref(String(route.query.email ?? '')); const status = ref(route.query.token ? 'Confirming your email...' : 'Check your email for a confirmation link.'); const error = ref(''); const resent = ref(false); const pending = ref(false); const showResend = ref(!route.query.token);
    onMounted(async () => { if (!route.query.token) return; try { const token = await issueAntiforgery(); await confirmEmail({ email: String(route.query.email ?? ''), token: String(route.query.token) }, { headers: { 'X-XSRF-TOKEN': token.token } }); status.value = 'Your email has been confirmed. You can sign in.'; showResend.value = false; } catch (exception) { error.value = exception instanceof Error ? exception.message : 'This confirmation link is invalid or expired.'; status.value = ''; showResend.value = true; } });
    async function resend() { pending.value = true; error.value = ''; try { const token = await issueAntiforgery(); await resendConfirmation({ email: email.value }, { headers: { 'X-XSRF-TOKEN': token.token } }); resent.value = true; } catch (exception) { error.value = exception instanceof Error ? exception.message : 'Unable to resend confirmation.'; } finally { pending.value = false; } }
    </script>
    <template><UiCard><div class="auth-card-heading"><p class="eyebrow">Account security</p><h1>Email confirmation</h1><p v-if="status" class="muted" v-text="status"></p><p v-if="error" class="form-error" role="alert" v-text="error"></p><form v-if="showResend" class="form-stack" @submit.prevent="resend"><label>Email<UiInput v-model="email" type="email" autocomplete="email" required /></label><UiButton type="submit" :disabled="pending"><span v-text="pending ? 'Sending...' : 'Resend confirmation email'"></span></UiButton><p v-if="resent" class="muted">If the account exists, a confirmation email has been sent.</p></form></div><p class="form-links"><RouterLink to="/auth/login">Continue to sign in</RouterLink></p></UiCard></template>
    """;

    private static string ProfilePage() => """
    <script setup lang="ts">
    import { onMounted, ref } from 'vue'; import UiCard from '../../components/ui/Card.vue'; import { me } from '../../dotisan/services'; const profile = ref<{ id: string; email: string }>(); const error = ref(''); onMounted(async () => { try { profile.value = await me(); } catch { error.value = 'Could not load your profile.'; } });
    </script>
    <template>
    <section class="page-stack"><div class="page-heading"><div><p class="eyebrow">Account</p><h2>Your profile</h2></div></div><UiCard><p v-if="error" class="form-error" role="alert" v-text="error"></p><dl v-else-if="profile" class="profile-list"><div><dt>Email</dt><dd v-text="profile.email"></dd></div><div><dt>User ID</dt><dd class="mono" v-text="profile.id"></dd></div></dl><p v-else class="muted">Loading profile...</p></UiCard></section>
    </template>
    """;

    private static string AuthorizationPage() => """
    <script setup lang="ts">
    import { onMounted, ref } from 'vue'; import UiCard from '../../components/ui/Card.vue'; import UiInput from '../../components/ui/Input.vue'; import { issueAntiforgery } from '../../dotisan/services';
    const users = ref<{ id: string; email: string }[]>([]); const roles = ref<string[]>([]); const selectedRole = ref(''); const error = ref('');
    async function load() { const response = await fetch('/api/authorization/users', { credentials: 'include' }); if (!response.ok) throw new Error(response.status === 403 ? 'You do not have permission to manage authorization.' : 'Could not load authorization data.'); const data = await response.json(); users.value = data.users; roles.value = data.roles; selectedRole.value = roles.value[0] ?? ''; }
    async function assign(userId: string) { if (!selectedRole.value) return; const token = await issueAntiforgery(); const response = await fetch(`/api/authorization/users/${userId}/roles/${encodeURIComponent(selectedRole.value)}`, { method: 'POST', credentials: 'include', headers: { 'X-XSRF-TOKEN': token.token } }); if (!response.ok) error.value = 'Could not assign that role.'; }
    onMounted(() => load().catch(exception => error.value = exception instanceof Error ? exception.message : 'Could not load authorization data.'));
    </script>
    <template><section class="page-stack"><div class="page-heading"><div><p class="eyebrow">Administration</p><h2>Authorization</h2><p class="muted">Assign existing Identity roles to users. Define permission claims in the API.</p></div></div><UiCard><p v-if="error" class="form-error" role="alert" v-text="error"></p><label>Role<UiInput v-model="selectedRole" placeholder="Role name" /></label><p v-if="!users.length" class="muted">Loading users...</p><ul v-else class="profile-list"><li v-for="user in users" :key="user.id"><span v-text="user.email"></span><button class="ui-button ui-button--outline" @click="assign(user.id)">Assign role</button></li></ul></UiCard></section></template>
    """;

    private static string MfaPage() => """
    <script setup lang="ts">
    import { onMounted, ref } from 'vue'; import UiButton from '../../components/ui/Button.vue'; import UiCard from '../../components/ui/Card.vue'; import UiInput from '../../components/ui/Input.vue'; import { getMfaSetup, regenerateRecoveryCodes, verifyMfa } from '../../dotisan/services';
    const sharedKey = ref(''); const uri = ref(''); const code = ref(''); const recoveryCodes = ref<string[]>([]); const message = ref(''); const error = ref('');
    onMounted(async () => { try { const setup = await getMfaSetup(); sharedKey.value = setup.sharedKey; uri.value = setup.authenticatorUri; } catch { error.value = 'Could not load MFA setup.'; } });
    async function enable() { try { await verifyMfa({ code: code.value }); message.value = 'MFA enabled.'; } catch { error.value = 'That authenticator code is invalid.'; } }
    async function regenerate() { try { const result = await regenerateRecoveryCodes(); recoveryCodes.value = result.recoveryCodes; } catch { error.value = 'Could not regenerate recovery codes.'; } }
    </script>
    <template><section class="page-stack"><div class="page-heading"><div><p class="eyebrow">Account security</p><h2>Multi-factor authentication</h2><p class="muted">Use an authenticator app and keep recovery codes somewhere safe.</p></div></div><UiCard><p v-if="error" class="form-error" role="alert" v-text="error"></p><p v-if="message" class="muted" v-text="message"></p><p><strong>Secret:</strong> <code v-text="sharedKey"></code></p><p class="muted">Authenticator URI: <code v-text="uri"></code></p><form class="form-stack" @submit.prevent="enable"><label>Verification code<UiInput v-model="code" inputmode="numeric" autocomplete="one-time-code" required /></label><UiButton type="submit">Enable MFA</UiButton></form><UiButton variant="outline" @click="regenerate">Generate recovery codes</UiButton><ul v-if="recoveryCodes.length"><li v-for="recoveryCode in recoveryCodes" :key="recoveryCode" class="mono" v-text="recoveryCode"></li></ul></UiCard></section></template>
    """;

    private static string SessionsPage() => """
    <script setup lang="ts">
    import { onMounted, ref } from 'vue'; import UiButton from '../../components/ui/Button.vue'; import UiCard from '../../components/ui/Card.vue'; import { issueAntiforgery, listSessions, revokeSession, revokeAllSessions } from '../../dotisan/services';
    const sessions = ref<{ id: string; deviceName: string; createdAt: string; lastSeenAt: string; expiresAt: string; userAgent?: string | null; ipAddress?: string | null }[]>([]); const error = ref(''); const message = ref('');
    async function load() { try { sessions.value = (await listSessions()).sessions; } catch { error.value = 'Could not load active sessions.'; } }
    async function revoke(id: string) { try { const token = await issueAntiforgery(); await revokeSession(id, { headers: { 'X-XSRF-TOKEN': token.token } }); await load(); } catch { error.value = 'Could not revoke that session.'; } }
    async function revokeAll() { try { const token = await issueAntiforgery(); await revokeAllSessions({ headers: { 'X-XSRF-TOKEN': token.token } }); message.value = 'All sessions have been revoked.'; await load(); } catch { error.value = 'Could not revoke sessions.'; } }
    onMounted(load);
    </script>
    <template><section class="page-stack"><div class="page-heading"><div><p class="eyebrow">Account security</p><h2>Sessions and devices</h2><p class="muted">Review where your account is signed in and revoke access you do not recognize.</p></div><UiButton variant="outline" @click="revokeAll">Revoke all</UiButton></div><UiCard><p v-if="error" class="form-error" role="alert" v-text="error"></p><p v-if="message" class="muted" v-text="message"></p><p v-if="!sessions.length" class="muted">No active sessions recorded.</p><ul v-else class="profile-list"><li v-for="session in sessions" :key="session.id"><strong v-text="session.deviceName"></strong><span class="muted" v-text="session.userAgent || 'Unknown browser'"></span><span class="muted" v-text="session.ipAddress || 'Unknown address'"></span><span class="muted">Expires <time :datetime="session.expiresAt" v-text="new Date(session.expiresAt).toLocaleString()"></time></span><UiButton variant="outline" @click="revoke(session.id)">Revoke</UiButton></li></ul></UiCard></section></template>
    """;

    private static string ExternalLoginsPage() => """
    <script setup lang="ts">
    import { onMounted, ref } from 'vue'; import UiButton from '../../components/ui/Button.vue'; import UiCard from '../../components/ui/Card.vue'; import { issueAntiforgery } from '../../dotisan/services';
    const providers = ref<{ name: string; displayName: string }[]>([]); const message = ref('');
    onMounted(async () => { const response = await fetch('/api/account/external/providers'); if (response.ok) providers.value = (await response.json()).providers; });
    function connect(name: string) { window.location.assign(`/api/account/external/${encodeURIComponent(name)}/challenge?returnUrl=${encodeURIComponent('/security/external-logins')}`); }
    async function disconnect(name: string) { const token = await issueAntiforgery(); await fetch(`/api/account/external/${encodeURIComponent(name)}/link`, { method: 'DELETE', credentials: 'include', headers: { 'X-XSRF-TOKEN': token.token } }); message.value = `${name} disconnected.`; }
    </script>
    <template><section class="page-stack"><div class="page-heading"><div><p class="eyebrow">Account security</p><h2>External sign-in providers</h2><p class="muted">Connect providers through an application-specific adapter. Dotisan keeps this boundary provider-neutral.</p></div></div><UiCard><p v-if="message" class="muted" v-text="message"></p><p v-if="!providers.length" class="muted">No external providers are configured yet.</p><ul v-else class="profile-list"><li v-for="provider in providers" :key="provider.name"><strong v-text="provider.displayName"></strong><span class="muted" v-text="provider.name"></span><UiButton @click="connect(provider.name)">Connect</UiButton><UiButton variant="outline" @click="disconnect(provider.name)">Disconnect</UiButton></li></ul></UiCard></section></template>
    """;

    private static string StyleCss() => """
    @tailwind base;
    @tailwind components;
    @tailwind utilities;

    :root { font-family: Inter, ui-sans-serif, system-ui, sans-serif; color: oklch(24% .03 255); background: oklch(97% .012 255); font-synthesis: none; }
    * { box-sizing: border-box; }
    body { margin: 0; min-width: 320px; background: oklch(97% .012 255); }
    button, input { font: inherit; }
    a { color: inherit; text-decoration: none; }
    .portal-layout { display: flex; min-height: 100vh; }
    .app-sidebar { display: flex; width: 16rem; flex-direction: column; gap: 2.5rem; padding: 1.5rem 1rem; background: oklch(24% .03 255); color: oklch(96% .01 255); }
    .brand, .auth-brand { display: flex; align-items: center; gap: .7rem; font-weight: 750; letter-spacing: -.02em; }
    .brand-mark { display: grid; width: 2rem; height: 2rem; place-items: center; border-radius: .6rem; background: oklch(76% .15 75); color: oklch(24% .03 255); font-weight: 850; }
    nav { display: grid; gap: .35rem; }
    .nav-link { border-radius: .6rem; padding: .7rem .8rem; color: oklch(82% .02 255); font-size: .9rem; transition: background .18s ease, color .18s ease; }
    .nav-link:hover, .nav-link--active { background: oklch(34% .04 255); color: oklch(99% .005 255); }
    .sidebar-footer { margin-top: auto; }
    .sidebar-caption, .muted { color: oklch(58% .025 255); font-size: .875rem; line-height: 1.55; }
    .app-header { display: flex; align-items: center; justify-content: space-between; gap: 1rem; border-bottom: 1px solid oklch(88% .02 255); padding: 1.4rem clamp(1.25rem, 4vw, 3rem); background: oklch(99% .004 255); }
    .app-header h1, .app-header p { margin: 0; }
    .app-header h1 { font-size: 1rem; }
    .header-kicker, .eyebrow { margin: 0 0 .4rem; color: oklch(55% .12 75); font-size: .72rem; font-weight: 800; letter-spacing: .12em; text-transform: uppercase; }
    .portal-main { flex: 1; min-width: 0; }
    .portal-content { width: min(100%, 80rem); margin: 0 auto; padding: clamp(1.5rem, 4vw, 3rem); }
    .page-stack { display: grid; gap: 1.5rem; }
    .page-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; }
    .page-heading h2 { margin: 0; font-size: clamp(1.8rem, 4vw, 2.7rem); letter-spacing: -.04em; }
    .page-lede { max-width: 60ch; margin: .65rem 0 0; color: oklch(52% .03 255); line-height: 1.6; }
    .dashboard-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1rem; }
    .ui-card { border: 1px solid oklch(88% .02 255); border-radius: .9rem; background: oklch(99% .004 255); box-shadow: 0 .8rem 2.5rem oklch(24% .03 255 / .05); }
    .ui-card__header, .ui-card__content, .ui-card__footer { padding: 1.25rem; }
    .ui-card__header { border-bottom: 1px solid oklch(92% .015 255); }
    .ui-card__footer { border-top: 1px solid oklch(92% .015 255); }
    .ui-card h1, .ui-card h3 { margin: 0; }
    .ui-card h1 { font-size: 1.7rem; letter-spacing: -.035em; }
    .metric { margin: 0; font-size: 1.5rem; font-weight: 750; letter-spacing: -.03em; }
    .ui-badge { display: inline-flex; align-items: center; border-radius: 999px; padding: .35rem .65rem; background: oklch(92% .06 75); color: oklch(38% .08 75); font-size: .75rem; font-weight: 750; }
    .ui-button { display: inline-flex; min-height: 2.5rem; align-items: center; justify-content: center; border: 1px solid transparent; border-radius: .55rem; padding: .55rem .85rem; cursor: pointer; font-size: .875rem; font-weight: 700; transition: background .18s ease, border-color .18s ease, opacity .18s ease; }
    .ui-button:disabled { cursor: not-allowed; opacity: .55; }
    .ui-button--default { background: oklch(30% .04 255); color: oklch(98% .005 255); }
    .ui-button--default:hover { background: oklch(38% .05 255); }
    .ui-button--outline { border-color: oklch(84% .02 255); background: transparent; color: oklch(30% .04 255); }
    .ui-button--ghost { background: transparent; color: inherit; }
    .ui-button--ghost:hover { background: oklch(92% .02 255 / .6); }
    .ui-button--destructive { background: oklch(54% .16 25); color: oklch(98% .005 25); }
    .ui-input { width: 100%; border: 1px solid oklch(82% .025 255); border-radius: .55rem; background: oklch(100% 0 0); padding: .65rem .75rem; color: oklch(24% .03 255); outline: none; }
    .ui-input:focus { border-color: oklch(55% .12 75); box-shadow: 0 0 0 .2rem oklch(76% .15 75 / .22); }
    .auth-layout { display: grid; min-height: 100vh; place-items: center; align-content: center; gap: 2rem; padding: 2rem 1rem; background: oklch(96% .018 255); }
    .auth-layout > .ui-card { width: min(100%, 28rem); }
    .auth-brand { color: oklch(30% .04 255); }
    .form-stack { display: grid; gap: 1rem; }
    .form-stack label { display: grid; gap: .4rem; color: oklch(35% .03 255); font-size: .875rem; font-weight: 650; }
    .checkbox { display: flex !important; grid-template-columns: auto 1fr; align-items: center; gap: .5rem !important; font-weight: 500 !important; }
    .form-error { margin: 0; border-radius: .55rem; padding: .7rem .8rem; background: oklch(94% .045 25); color: oklch(40% .12 25); font-size: .875rem; }
    .field-error { margin: -.55rem 0 0; color: oklch(43% .12 25); font-size: .78rem; line-height: 1.35; }
    .ui-input[aria-invalid="true"] { border-color: oklch(58% .16 25); }
    .form-links { display: flex; flex-wrap: wrap; justify-content: space-between; gap: .75rem; margin: 0; color: oklch(45% .08 75); font-size: .8rem; font-weight: 700; }
    .user-menu { position: relative; }
    .user-menu__panel { position: absolute; z-index: 2; right: 0; display: grid; min-width: 9rem; gap: .2rem; margin-top: .25rem; border: 1px solid oklch(88% .02 255); border-radius: .6rem; padding: .35rem; background: oklch(99% .004 255); box-shadow: 0 .8rem 2rem oklch(24% .03 255 / .12); }
    .user-menu__panel a { padding: .5rem; font-size: .85rem; }
    .user-menu__panel .ui-button { justify-content: flex-start; }
    .profile-list { display: grid; gap: 1rem; margin: 0; }
    .profile-list div { display: grid; gap: .25rem; }
    .profile-list dt { color: oklch(55% .025 255); font-size: .75rem; font-weight: 750; text-transform: uppercase; }
    .profile-list dd { margin: 0; }
    .mono { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: .8rem; overflow-wrap: anywhere; }
    code { border-radius: .3rem; background: oklch(93% .015 255); padding: .15rem .3rem; }
    @media (max-width: 720px) { .portal-layout { display: block; } .app-sidebar { width: auto; gap: 1rem; padding: 1rem; } .app-sidebar nav { display: flex; overflow-x: auto; } .app-sidebar .sidebar-footer { display: none; } .dashboard-grid { grid-template-columns: 1fr; } .page-heading { display: grid; } }
    """;

    private static string Dockerfile(string name, PackageManager packageManager) => $$"""
    FROM node:22-alpine AS web-build
    WORKDIR /src
    {{(packageManager == PackageManager.Pnpm ? $"RUN corepack enable && corepack prepare pnpm@9.15.5 --activate\n    COPY src/{name}.Web/package.json src/{name}.Web/pnpm-lock.yaml ./\n    RUN pnpm install --frozen-lockfile" : $"COPY src/{name}.Web/package*.json ./\n    RUN npm ci")}}
    COPY src/{{name}}.Web/ ./
    RUN npm run build

    FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
    WORKDIR /src
    COPY src/{{name}}.Api/{{name}}.Api.csproj src/{{name}}.Api/
    RUN dotnet restore src/{{name}}.Api/{{name}}.Api.csproj
    COPY . .
    COPY --from=web-build /src/dist src/{{name}}.Api/wwwroot
    RUN dotnet publish src/{{name}}.Api/{{name}}.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

    FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
    WORKDIR /app
    COPY --from=api-build /app/publish .
    USER $APP_UID
    EXPOSE 8080
    HEALTHCHECK --interval=30s --timeout=5s CMD wget --spider --no-verbose http://localhost:8080/api/health || exit 1
    ENTRYPOINT ["dotnet", "{{name}}.Api.dll"]
    """;

    private static string Notifications(string identifier, bool authenticationEnabled, bool multiTenancyEnabled) => $$"""
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using Microsoft.AspNetCore.Routing;
    using {{identifier}}.Api.Data;
    {{(authenticationEnabled ? $"using {identifier}.Api.Authorization;\n    using Microsoft.AspNetCore.Antiforgery;" : string.Empty)}}
    {{(multiTenancyEnabled ? $"using {identifier}.Api.Tenancy;" : string.Empty)}}

    namespace {{identifier}}.Api.Integrations;

    public sealed class NotificationRecord
    {
        public Guid Id { get; set; }
        public string RecipientId { get; set; } = string.Empty;
        public string? TenantId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
    }
    public sealed class NotificationRecordConfiguration : IEntityTypeConfiguration<NotificationRecord>
    {
        public void Configure(EntityTypeBuilder<NotificationRecord> builder) { builder.HasKey(item => item.Id); builder.HasIndex(item => new { item.TenantId, item.RecipientId, item.IsRead }); builder.Property(item => item.Title).HasMaxLength(200); builder.Property(item => item.Body).HasMaxLength(4000); }
    }
    public interface INotificationStore
    {
        Task<IReadOnlyList<NotificationRecord>> ListAsync(string recipientId, string? tenantId, CancellationToken cancellationToken);
        Task<NotificationRecord> AddAsync(string recipientId, string? tenantId, string title, string body, CancellationToken cancellationToken);
        Task<bool> MarkReadAsync(Guid id, string recipientId, string? tenantId, CancellationToken cancellationToken);
    }
    public sealed class EfNotificationStore(AppDbContext db) : INotificationStore
    {
        public async Task<IReadOnlyList<NotificationRecord>> ListAsync(string recipientId, string? tenantId, CancellationToken cancellationToken) => await db.Notifications.AsNoTracking().Where(item => item.RecipientId == recipientId && item.TenantId == tenantId).OrderByDescending(item => item.CreatedAtUtc).ToListAsync(cancellationToken);
        public async Task<NotificationRecord> AddAsync(string recipientId, string? tenantId, string title, string body, CancellationToken cancellationToken) { var item = new NotificationRecord { Id = Guid.NewGuid(), RecipientId = recipientId, TenantId = tenantId, Title = title, Body = body, CreatedAtUtc = DateTimeOffset.UtcNow }; db.Notifications.Add(item); await db.SaveChangesAsync(cancellationToken); return item; }
        public async Task<bool> MarkReadAsync(Guid id, string recipientId, string? tenantId, CancellationToken cancellationToken) { var item = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.RecipientId == recipientId && x.TenantId == tenantId, cancellationToken); if (item is null) return false; item.IsRead = true; await db.SaveChangesAsync(cancellationToken); return true; }
    }
    public sealed class NotificationHub : Hub { }
    public sealed class NotificationUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection) => connection.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }
    public static class NotificationEndpoints
    {
        public static void Map(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/notifications"); {{(authenticationEnabled ? "group.RequireAuthorization();" : string.Empty)}}
            group.MapGet("", async (HttpContext httpContext, INotificationStore store{{(multiTenancyEnabled ? ", ITenantContext tenantContext" : string.Empty)}}, CancellationToken cancellationToken) => Results.Ok(new { notifications = await store.ListAsync(httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous", {{(multiTenancyEnabled ? "tenantContext.TenantId" : "null")}}, cancellationToken) }));
            group.MapPost("", async (NotificationRequest request, HttpContext httpContext, INotificationStore store, IHubContext<NotificationHub> hub{{(multiTenancyEnabled ? ", ITenantContext tenantContext" : string.Empty)}}, CancellationToken cancellationToken) => { if (string.IsNullOrWhiteSpace(request.RecipientId) || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["notification"] = ["Recipient, title, and body are required."] }); var item = await store.AddAsync(request.RecipientId.Trim(), {{(multiTenancyEnabled ? "tenantContext.TenantId" : "null")}}, request.Title.Trim(), request.Body.Trim(), cancellationToken); await hub.Clients.User(item.RecipientId).SendAsync("notification", item, cancellationToken); return Results.Created($"/api/notifications/{item.Id}", item); }){{(authenticationEnabled ? ".RequireAuthorization(Permissions.AuthorizationManage).WithMetadata(new RequireAntiforgeryTokenAttribute(true))" : string.Empty)}};
            group.MapPost("/{id:guid}/read", async (Guid id, HttpContext httpContext, INotificationStore store{{(multiTenancyEnabled ? ", ITenantContext tenantContext" : string.Empty)}}, CancellationToken cancellationToken) => await store.MarkReadAsync(id, httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous", {{(multiTenancyEnabled ? "tenantContext.TenantId" : "null")}}, cancellationToken) ? Results.NoContent() : Results.NotFound()){{(authenticationEnabled ? ".WithMetadata(new RequireAntiforgeryTokenAttribute(true))" : string.Empty)}};
        }
        public sealed record NotificationRequest(string RecipientId, string Title, string Body);
    }
    """;

    private static string Storage(string identifier, bool authenticationEnabled, bool multiTenancyEnabled) => $$"""
    using Microsoft.AspNetCore.Routing;
    namespace {{identifier}}.Api.Integrations;

    public sealed record StoredFile(string Key, string ContentType, long Length, DateTimeOffset CreatedAtUtc);
    public interface IFileStorage
    {
        Task<StoredFile> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);
        Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);
        Task DeleteAsync(string key, CancellationToken cancellationToken = default);
    }
    public sealed class LocalFileStorage(IHostEnvironment environment, IConfiguration configuration) : IFileStorage
    {
        private string Resolve(string key) { var root = Path.GetFullPath(configuration["Storage:LocalRoot"] ?? Path.Combine(environment.ContentRootPath, "storage")); var path = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar))); if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid storage key."); return path; }
        public async Task<StoredFile> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default) { var maxBytes = configuration.GetValue<long>("Storage:MaxBytes", 10_485_760); var path = Resolve(key); Directory.CreateDirectory(Path.GetDirectoryName(path)!); await using var target = File.Create(path); await content.CopyToAsync(target, cancellationToken); if (target.Length > maxBytes) { target.Close(); File.Delete(path); throw new InvalidDataException("File exceeds configured size limit."); } return new StoredFile(key, contentType, target.Length, DateTimeOffset.UtcNow); }
        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default) { var path = Resolve(key); return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null); }
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) { var path = Resolve(key); if (File.Exists(path)) File.Delete(path); return Task.CompletedTask; }
    }
    public static class StorageEndpoints
    {
        public static void Map(IEndpointRouteBuilder endpoints) { var group = endpoints.MapGroup("/api/files"); {{(authenticationEnabled ? "group.RequireAuthorization();" : string.Empty)}} group.MapPost("", async (IFormFile file, IFileStorage storage, CancellationToken cancellationToken) => { if (file.Length == 0 || string.IsNullOrWhiteSpace(file.ContentType)) return Results.BadRequest(new { code = "invalid_file" }); var key = $"uploads/{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}"; await using var stream = file.OpenReadStream(); return Results.Ok(await storage.PutAsync(key, stream, file.ContentType, cancellationToken)); }){{(authenticationEnabled ? ".WithMetadata(new RequireAntiforgeryTokenAttribute(true))" : string.Empty)}}; group.MapGet("/{**key}", async (string key, IFileStorage storage, CancellationToken cancellationToken) => { var stream = await storage.OpenReadAsync(key, cancellationToken); return stream is null ? Results.NotFound() : Results.File(stream, "application/octet-stream"); }); group.MapDelete("/{**key}", async (string key, IFileStorage storage, CancellationToken cancellationToken) => { await storage.DeleteAsync(key, cancellationToken); return Results.NoContent(); }){{(authenticationEnabled ? ".WithMetadata(new RequireAntiforgeryTokenAttribute(true))" : string.Empty)}}; }
    }
    """;

    private static string Caching(string identifier) => $$"""
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    namespace {{identifier}}.Api.Integrations;

    public interface IApplicationCache
    {
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
        Task SetAsync<T>(string key, T value, TimeSpan duration, CancellationToken cancellationToken = default);
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    }
    public interface IDistributedApplicationCache : IApplicationCache { }
    public sealed partial class MemoryApplicationCache(IMemoryCache cache, ILogger<MemoryApplicationCache> logger) : IDistributedApplicationCache
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) { cache.TryGetValue(key, out T? value); CacheLookup(logger, key, value is null ? "miss" : "hit"); return Task.FromResult(value); }
        public Task SetAsync<T>(string key, T value, TimeSpan duration, CancellationToken cancellationToken = default) { cache.Set(key, value, duration); return Task.CompletedTask; }
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) { cache.Remove(key); return Task.CompletedTask; }

        [LoggerMessage(Level = LogLevel.Debug, Message = "Cache {CacheKey} {CacheResult}")]
        private static partial void CacheLookup(ILogger logger, string cacheKey, string cacheResult);
    }
    public static class CacheKeys { public static string ForTenant(string tenantId, string resource, string key) => $"tenant:{tenantId}:{resource}:{key}"; }
    """;

    private static string ImportsExports(string identifier, bool authenticationEnabled, bool multiTenancyEnabled) => $$"""
    using System.Text;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.EntityFrameworkCore;
    using {{identifier}}.Api.Data;
    {{(authenticationEnabled ? "using Microsoft.AspNetCore.Antiforgery;" : string.Empty)}}
    {{(multiTenancyEnabled ? $"using {identifier}.Api.Tenancy;" : string.Empty)}}
    using Wolverine;
    namespace {{identifier}}.Api.Integrations;

    public sealed record ImportJob(Guid Id, string Format, string Status, int Processed, int Failed, DateTimeOffset CreatedAtUtc);
    public sealed record ImportStatus(Guid Id, string Status, int Processed, int Failed, string? ValidationReport);
    public sealed class ImportRecord { public Guid Id { get; set; } public string Format { get; set; } = string.Empty; public string Status { get; set; } = "queued"; public int Processed { get; set; } public int Failed { get; set; } public string? ValidationReport { get; set; } public string? TenantId { get; set; } public DateTimeOffset CreatedAtUtc { get; set; } }
    public interface IDataExchangeService
    {
        Task<ImportJob> StartImportAsync(Stream content, string format, CancellationToken cancellationToken = default);
        Task<ImportStatus?> GetStatusAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Stream> ExportAsync(string format, CancellationToken cancellationToken = default);
    }
    public sealed record ImportRequested(Guid Id, string Format, string? TenantId, byte[] Content);
    public sealed class DataExchangeService(AppDbContext db, IMessageBus bus{{(multiTenancyEnabled ? ", ITenantContext tenantContext" : string.Empty)}}) : IDataExchangeService
    {
        public async Task<ImportJob> StartImportAsync(Stream content, string format, CancellationToken cancellationToken = default) { if (format is not ("csv" or "json")) throw new ArgumentException("Only csv and json imports are supported.", nameof(format)); using var memory = new MemoryStream(); await content.CopyToAsync(memory, cancellationToken); if (memory.Length > 10_485_760) throw new InvalidDataException("Import exceeds the 10 MB limit."); var id = Guid.NewGuid(); var record = new ImportRecord { Id = id, Format = format, TenantId = {{(multiTenancyEnabled ? "tenantContext.RequireTenantId()" : "null")}}, CreatedAtUtc = DateTimeOffset.UtcNow }; db.ImportRecords.Add(record); await db.SaveChangesAsync(cancellationToken); await bus.PublishAsync(new ImportRequested(id, format, record.TenantId, memory.ToArray())); return new ImportJob(id, format, record.Status, 0, 0, record.CreatedAtUtc); }
        public async Task<ImportStatus?> GetStatusAsync(Guid id, CancellationToken cancellationToken = default) { var query = db.ImportRecords.AsNoTracking().Where(item => item.Id == id); {{(multiTenancyEnabled ? "var tenantId = tenantContext.RequireTenantId(); query = query.Where(item => item.TenantId == tenantId);" : string.Empty)}} var record = await query.SingleOrDefaultAsync(cancellationToken); return record is null ? null : new ImportStatus(record.Id, record.Status, record.Processed, record.Failed, record.ValidationReport); }
        public Task<Stream> ExportAsync(string format, CancellationToken cancellationToken = default) => format is "csv" or "json" ? Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(format == "csv" ? "id,name\n" : "[]"))) : throw new ArgumentException("Only csv and json exports are supported.", nameof(format));
    }
    public sealed class ImportRequestedHandler(AppDbContext db)
    {
        public async Task Handle(ImportRequested message, CancellationToken cancellationToken) { var record = await db.ImportRecords.SingleOrDefaultAsync(item => item.Id == message.Id, cancellationToken); if (record is null) return; record.Status = "completed"; record.Processed = 0; await db.SaveChangesAsync(cancellationToken); }
    }
    public static class ImportExportEndpoints
    {
        public static void Map(IEndpointRouteBuilder endpoints) { var group = endpoints.MapGroup("/api/data"); {{(authenticationEnabled ? "group.RequireAuthorization();" : string.Empty)}} group.MapPost("/imports", async (IFormFile file, IDataExchangeService service, CancellationToken cancellationToken) => { if (file.Length == 0 || file.Length > 10_485_760) return Results.BadRequest(new { code = "invalid_import_size" }); return Results.Accepted(value: await service.StartImportAsync(file.OpenReadStream(), Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant(), cancellationToken)); }){{(authenticationEnabled ? ".WithMetadata(new RequireAntiforgeryTokenAttribute(true))" : string.Empty)}}; group.MapGet("/imports/{id:guid}", async (Guid id, IDataExchangeService service, CancellationToken cancellationToken) => { var status = await service.GetStatusAsync(id, cancellationToken); return status is null ? Results.NotFound() : Results.Ok(status); }); group.MapGet("/exports/{format}", async (string format, IDataExchangeService service, CancellationToken cancellationToken) => Results.File(await service.ExportAsync(format, cancellationToken), format == "csv" ? "text/csv" : "application/json", $"export.{format}")); }
    }
    """;

    private static string Webhooks(string identifier, bool authenticationEnabled, bool multiTenancyEnabled) => $$"""
    using System.Text.Json;
    using System.Security.Cryptography;
    using System.Text;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.EntityFrameworkCore;
    using {{identifier}}.Api.Data;
    {{(authenticationEnabled ? $"using {identifier}.Api.Security;" : string.Empty)}}
    namespace {{identifier}}.Api.Integrations;

    public sealed class WebhookDelivery { public Guid Id { get; set; } public string EventType { get; set; } = string.Empty; public string Endpoint { get; set; } = string.Empty; public string Signature { get; set; } = string.Empty; public string PayloadJson { get; set; } = "{}"; public string Status { get; set; } = "queued"; public int Attempts { get; set; } public int RetryCount { get; set; } public DateTimeOffset CreatedAtUtc { get; set; } }
    public sealed record WebhookSubscription(Guid Id, string EventType, Uri Endpoint, bool Enabled);
    public interface IWebhookDispatcher
    {
        Task DispatchAsync(string eventType, object payload, CancellationToken cancellationToken = default);
        Task<bool> ReplayAsync(Guid deliveryId, CancellationToken cancellationToken = default);
    }
    public sealed class HmacWebhookDispatcher(AppDbContext db, IHttpClientFactory clients, IConfiguration configuration) : IWebhookDispatcher
    {
        public async Task DispatchAsync(string eventType, object payload, CancellationToken cancellationToken = default) { var body = JsonSerializer.Serialize(payload); var secret = configuration["Webhooks:SigningSecret"] ?? throw new InvalidOperationException("Webhooks:SigningSecret must be configured."); var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))); var maxAttempts = Math.Clamp(configuration.GetValue("Webhooks:MaxAttempts", 3), 1, 10); foreach (var endpoint in configuration.GetSection("Webhooks:Endpoints").Get<string[]>() ?? []) { if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var destination) || destination.Scheme is not ("http" or "https")) throw new InvalidOperationException("Webhooks:Endpoints must contain absolute HTTP(S) URLs."); var delivery = new WebhookDelivery { Id = Guid.NewGuid(), EventType = eventType, Endpoint = destination.ToString(), Signature = signature, PayloadJson = body, CreatedAtUtc = DateTimeOffset.UtcNow }; db.WebhookDeliveries.Add(delivery); for (var attempt = 1; attempt <= maxAttempts; attempt++) { delivery.Attempts = attempt; try { using var request = new HttpRequestMessage(HttpMethod.Post, destination) { Content = new StringContent(body, Encoding.UTF8, "application/json") }; request.Headers.Add("X-Dotisan-Signature", signature); using var response = await clients.CreateClient().SendAsync(request, cancellationToken); if (response.IsSuccessStatusCode) { delivery.Status = "delivered"; break; } delivery.Status = "failed"; } catch { delivery.Status = "failed"; } if (attempt < maxAttempts) { delivery.RetryCount++; await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), cancellationToken); } } await db.SaveChangesAsync(cancellationToken); } }
        public async Task<bool> ReplayAsync(Guid deliveryId, CancellationToken cancellationToken = default) { var delivery = await db.WebhookDeliveries.SingleOrDefaultAsync(item => item.Id == deliveryId, cancellationToken); if (delivery is null) return false; var secret = configuration["Webhooks:SigningSecret"] ?? throw new InvalidOperationException("Webhooks:SigningSecret must be configured."); var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(delivery.PayloadJson))); using var request = new HttpRequestMessage(HttpMethod.Post, delivery.Endpoint) { Content = new StringContent(delivery.PayloadJson, Encoding.UTF8, "application/json") }; request.Headers.Add("X-Dotisan-Signature", signature); delivery.Signature = signature; using var response = await clients.CreateClient().SendAsync(request, cancellationToken); delivery.Attempts++; delivery.RetryCount++; delivery.Status = response.IsSuccessStatusCode ? "delivered" : "failed"; await db.SaveChangesAsync(cancellationToken); return response.IsSuccessStatusCode; }
    }
    public static class WebhookEndpoints
    {
        public static void Map(IEndpointRouteBuilder endpoints) { var group = endpoints.MapGroup("/api/webhooks"); {{(authenticationEnabled ? "group.RequireAuthorization();" : string.Empty)}} group.MapGet("/deliveries", async (AppDbContext db, CancellationToken cancellationToken) => Results.Ok(await db.WebhookDeliveries.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync(cancellationToken))); group.MapPost("/deliveries/{id:guid}/replay", async (Guid id, IWebhookDispatcher dispatcher, CancellationToken cancellationToken) => await dispatcher.ReplayAsync(id, cancellationToken) ? Results.Accepted($"/api/webhooks/deliveries/{id}", new { deliveryId = id, status = "delivered" }) : Results.NotFound()){{(authenticationEnabled ? ".RequireAuthorization(Permissions.AuthorizationManage)" : string.Empty)}}; }
    }
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
