using Dotisan.Core;
using Dotisan.Generators;

namespace Dotisan.Generators.Tests;

public sealed class GoldenTemplateGeneratorTests
{
    [Fact]
    public async Task Mailpit_provider_generates_compose_service_and_provider_configuration()
    {
        var output = Path.Combine(Path.GetTempPath(), "dotisan-mailpit-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = await new GoldenTemplateGenerator().GenerateAsync(ProjectOptions.Quick("MailApp", output) with
            {
                AuthenticationEnabled = true,
                MailProvider = MailProvider.Mailpit
            }, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            var compose = await File.ReadAllTextAsync(Path.Combine(output, "compose.yaml"));
            var config = await File.ReadAllTextAsync(Path.Combine(output, "dotisan.config"));
            var appsettings = await File.ReadAllTextAsync(Path.Combine(output, "src", "MailApp.Api", "appsettings.Development.json"));
            Assert.Contains("mailpit:", compose);
            Assert.Contains("axllent/mailpit", compose);
            Assert.Contains("8025:8025", compose);
            Assert.Contains("mail_provider: mailpit", config);
            Assert.Contains("\"Provider\": \"mailpit\"", appsettings);
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }
    [Fact]
    public async Task Generator_writes_buildable_app_shape_with_sqlite_defaults()
    {
        var output = Path.Combine(Path.GetTempPath(), "dotisan-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = await new GoldenTemplateGenerator().GenerateAsync(
                ProjectOptions.Quick("TodoApp", output), CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(File.Exists(Path.Combine(output, "TodoApp.sln")));
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Api", "Program.cs")));
            Assert.True(File.Exists(Path.Combine(output, "dotisan.contract.json")));
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Web", "package.json")));
            var generatedModels = Path.Combine(output, "src", "TodoApp.Web", "src", "dotisan", "models.ts");
            Assert.True(File.Exists(generatedModels));
            Assert.Equal(Environment.NewLine, await File.ReadAllTextAsync(generatedModels));
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Web", "src", "dotisan", "schemas.ts")));
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Web", "src", "dotisan", "services.ts")));
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Web", "src", "dotisan", "queries.ts")));
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Web", "src", "routes", "index.ts")));
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Web", "src", "components", "ui", "Button.vue")));
            Assert.Contains("\"skipLibCheck\": true", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Web", "tsconfig.app.json")));
            Assert.Contains("\"skipLibCheck\": true", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Web", "tsconfig.node.json")));
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Web", "src", "layouts", "PortalLayout.vue")));
            Assert.Contains("proxy: { '/api': 'http://localhost:", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Web", "vite.config.ts")));
            var mainTs = await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Web", "src", "main.ts"));
            Assert.Contains("VueQueryPlugin", mainTs);
            var program = await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "Program.cs"));
            Assert.DoesNotContain("DOTISAN_CONTRACT_FALLBACK", program);
            Assert.Contains("\"version\": \"0.8.6\"", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Web", "package.json")));
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Api", "Auditing", "AuditEntry.cs")));
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Api", "Auditing", "IAuditWriter.cs")));
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Api", "Auditing", "AuditWriter.cs")));
            var auditWriter = await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "Auditing", "AuditWriter.cs"));
            Assert.Contains("RecordAsync", auditWriter);
            Assert.Contains("Activity.Current", auditWriter);
            Assert.Contains("X-Correlation-ID", auditWriter);
            Assert.Contains("TraceIdentifier", auditWriter);
            Assert.Contains("ClaimTypes.NameIdentifier", auditWriter);
            Assert.Contains("JsonSerializer.Serialize", auditWriter);
            Assert.Contains("GetValue(\"Audit:Enabled\", true)", auditWriter);
            Assert.Contains("public sealed class AuditEntry", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "Auditing", "AuditEntry.cs")));
            Assert.Contains("DbSet<AuditEntry> AuditEntries", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "Data", "AppDbContext.cs")));
            Assert.Contains("\"Audit\":", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "appsettings.json")));
            Assert.Contains("\"Enabled\": true", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "appsettings.json")));
            Assert.Contains("AddScoped<IAuditWriter, AuditWriter>", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "Program.cs")));
            Assert.Contains("UseSqlite", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "Program.cs")));
            Assert.Contains("Microsoft.AspNetCore.OpenApi", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "TodoApp.Api.csproj")));
            var apiProject = await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "TodoApp.Api.csproj"));
            Assert.DoesNotContain("<PackageReference Include=\"Microsoft.OpenApi\"", apiProject);
            Assert.DoesNotContain("<PackageReference Include=\"SQLitePCLRaw.lib.e_sqlite3\"", apiProject);
            Assert.Contains("AddOpenApi", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "Program.cs")));
            Assert.Contains("MapOpenApi", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "Program.cs")));
            Assert.Contains("OpenTelemetry.Extensions.Hosting", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "TodoApp.Api.csproj")));
            Assert.Contains("OpenTelemetry.Exporter.OpenTelemetryProtocol", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "TodoApp.Api.csproj")));
            Assert.Contains("AddAspNetCoreInstrumentation", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "Program.cs")));
            Assert.Contains("AddHttpClientInstrumentation", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "Program.cs")));
            Assert.Contains("\"OpenTelemetry\"", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "appsettings.json")));
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Api", "Jobs", "JobRegistration.cs")));
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Api", "Jobs", "SampleJob.cs")));
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Api", "Jobs", "SampleJobHandler.cs")));
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Api", "Features", "Jobs", "JobEndpoints.cs")));
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Api", "Features", "Health", "HealthEndpoints.cs")));
            var jobs = await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "Jobs", "JobRegistration.cs"));
            Assert.Contains("UseDurableLocalQueues", jobs);
            Assert.Contains("PersistMessagesWithSqlite", jobs);
            Assert.Contains("DOTISAN:SCHEDULE sample|SampleJob|300|true", jobs);
            Assert.Contains("SampleJobScheduleStarter", jobs);
            Assert.Contains("IServiceScopeFactory", jobs);
            Assert.DoesNotContain("SampleJobScheduleStarter(IMessageBus bus", jobs);
            Assert.Contains("if (jobsEnabled)", jobs);
            Assert.Contains("RetryDelaySeconds", jobs);
            var jobHandler = await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "Jobs", "SampleJobHandler.cs"));
            Assert.Contains("ScheduleAsync", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "Features", "Jobs", "JobEndpoints.cs")));
            var jobTests = await File.ReadAllTextAsync(Path.Combine(output, "tests", "TodoApp.Api.Tests", "JobTests.cs"));
            Assert.Contains("IMessageBus", jobTests);
            Assert.Contains("SendAsync", jobTests);
            Assert.Contains("scheduled", jobTests, StringComparison.OrdinalIgnoreCase);
            var endpointRegistry = await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "Infrastructure", "DotisanEndpointExtensions.cs"));
            Assert.Contains("JobEndpoints.MapJobEndpoints", endpointRegistry);
            Assert.Contains("HealthEndpoints.MapHealthEndpoints", endpointRegistry);
            var generatedPaths = Directory.GetFiles(output, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(output, path).Replace(Path.DirectorySeparatorChar, '/'));
            Assert.DoesNotContain("src/TodoApp.Api/Jobs/JobEndpoints.cs", generatedPaths);
            Assert.Contains("UseWolverine", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "Program.cs")));
            Assert.Contains("\"Jobs\":", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "appsettings.json")));
            Assert.Contains("Data Source=Data/TodoApp.db", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "appsettings.json")));
            var config = await File.ReadAllTextAsync(Path.Combine(output, "dotisan.config"));
            var vite = await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Web", "vite.config.ts"));
            Assert.Contains("api_port:", config);
            Assert.Contains("proxy: { '/api': 'http://localhost:", vite);
            Assert.Contains("profile: quick", await File.ReadAllTextAsync(Path.Combine(output, "dotisan.config")));
            var readme = await File.ReadAllTextAsync(Path.Combine(output, "README.md"));
            Assert.Contains("AuditEntry", readme);
            Assert.Contains("Audit__Enabled", readme);
            Assert.Contains("InitialAudit", readme);
            Assert.Contains("make:crud", readme);
            Assert.Contains("checks the .NET SDK", readme);
            Assert.Contains("Press Ctrl+C once", readme);
            Assert.Contains("componentized dashboard shell", readme);
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Authenticated_template_includes_componentized_auth_pages_and_portal_routes()
    {
        var output = Path.Combine(Path.GetTempPath(), "dotisan-auth-vue-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = await new GoldenTemplateGenerator().GenerateAsync(
                ProjectOptions.Quick("AuthApp", output) with { AuthenticationEnabled = true }, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(File.Exists(Path.Combine(output, "src", "AuthApp.Web", "src", "pages", "auth", "LoginPage.vue")));
            Assert.True(File.Exists(Path.Combine(output, "src", "AuthApp.Web", "src", "pages", "account", "ProfilePage.vue")));
            var routes = await File.ReadAllTextAsync(Path.Combine(output, "src", "AuthApp.Web", "src", "routes", "index.ts"));
            Assert.Contains("requiresAuth", routes);
            Assert.Contains("path: 'login'", routes);
            var readme = await File.ReadAllTextAsync(Path.Combine(output, "README.md"));
            Assert.Contains("login, registration, forgot-password, and profile pages", readme);
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task Tenant_enabled_template_emits_tenant_context_and_resource_isolation_boundary()
    {
        var output = Path.Combine(Path.GetTempPath(), "dotisan-tenant-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var generated = await new GoldenTemplateGenerator().GenerateAsync(
                ProjectOptions.Quick("TenantApp", output) with { MultiTenancyEnabled = true }, CancellationToken.None);
            Assert.True(generated.Success, generated.ErrorMessage);

            var tenancy = await File.ReadAllTextAsync(Path.Combine(output, "src", "TenantApp.Api", "Tenancy", "TenantContext.cs"));
            var program = await File.ReadAllTextAsync(Path.Combine(output, "src", "TenantApp.Api", "Program.cs"));
            Assert.Contains("interface ITenantContext", tenancy);
            Assert.Contains("X-Tenant-ID", tenancy);
            Assert.Contains("AddHttpContextAccessor", program);
            Assert.Contains("AddScoped<ITenantContext, TenantContext>", program);

            var resource = await ResourceScaffolder.ScaffoldAsync(output, "Customer", CancellationToken.None);
            Assert.True(resource.Success, resource.ErrorMessage);
            var model = await File.ReadAllTextAsync(Path.Combine(output, "src", "TenantApp.Api", "Features", "Customers", "Customer.cs"));
            var endpoints = await File.ReadAllTextAsync(Path.Combine(output, "src", "TenantApp.Api", "Features", "Customers", "CustomerEndpoints.cs"));
            Assert.Contains("ITenantEntity", model);
            Assert.Contains("TenantId", model);
            Assert.Contains("RequireTenantId", endpoints);
            Assert.Contains("TenantId == tenantContext.TenantId", endpoints);
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task Generated_web_scaffold_contains_frontend_testing_and_shadcn_configuration()
    {
        var output = Path.Combine(Path.GetTempPath(), "dotisan-web-scaffold-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var generated = await new GoldenTemplateGenerator().GenerateAsync(ProjectOptions.Quick("WebScaffold", output), CancellationToken.None);
            Assert.True(generated.Success, generated.ErrorMessage);
            var web = Path.Combine(output, "src", "WebScaffold.Web");
            var package = await File.ReadAllTextAsync(Path.Combine(web, "package.json"));
            Assert.Contains("@vue/test-utils", package);
            Assert.Contains("@playwright/test", package);
            Assert.True(File.Exists(Path.Combine(web, "vitest.config.ts")));
            Assert.True(File.Exists(Path.Combine(web, "playwright.config.ts")));
            Assert.True(File.Exists(Path.Combine(web, "components.json")));
            Assert.True(File.Exists(Path.Combine(web, "src", "components", "ui", "Badge.test.ts")));
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task Email_confirmation_page_fetches_antiforgery_before_confirming()
    {
        var output = Path.Combine(Path.GetTempPath(), "dotisan-email-confirmation-page-" + Guid.NewGuid().ToString("N"));
        try
        {
            var generated = await new GoldenTemplateGenerator().GenerateAsync(ProjectOptions.Quick("ConfirmApp", output) with
            {
                AuthenticationEnabled = true
            }, CancellationToken.None);
            Assert.True(generated.Success, generated.ErrorMessage);

            var page = await File.ReadAllTextAsync(Path.Combine(output, "src", "ConfirmApp.Web", "src", "pages", "auth", "EmailConfirmationPage.vue"));
            Assert.Contains("const token = await issueAntiforgery();", page);
            Assert.Contains("confirmEmail({ email: String(route.query.email ?? ''), token: String(route.query.token) }, { headers: { 'X-XSRF-TOKEN': token.token } })", page);
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task Auth_pages_render_structured_validation_errors()
    {
        var output = Path.Combine(Path.GetTempPath(), "dotisan-auth-errors-" + Guid.NewGuid().ToString("N"));
        try
        {
            var generated = await new GoldenTemplateGenerator().GenerateAsync(
                ProjectOptions.Quick("AuthErrorsApp", output) with { AuthenticationEnabled = true }, CancellationToken.None);
            Assert.True(generated.Success, generated.ErrorMessage);

            var login = await File.ReadAllTextAsync(Path.Combine(output, "src", "AuthErrorsApp.Web", "src", "pages", "auth", "LoginPage.vue"));
            var register = await File.ReadAllTextAsync(Path.Combine(output, "src", "AuthErrorsApp.Web", "src", "pages", "auth", "RegisterPage.vue"));

            Assert.Contains("ApiError", login);
            Assert.Contains("fieldErrors", login);
            Assert.Contains("field-error", login);
            Assert.Contains("fieldErrors", register);
            Assert.Contains("field-error", register);
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task V080_integrations_are_opt_in_and_generate_provider_neutral_contracts()
    {
        var output = Path.Combine(Path.GetTempPath(), "dotisan-v080-integrations-" + Guid.NewGuid().ToString("N"));
        try
        {
            var generated = await new GoldenTemplateGenerator().GenerateAsync(ProjectOptions.Quick("IntegrationApp", output) with
            {
                AuthenticationEnabled = true,
                Registration = RegistrationPolicy.Public,
                NotificationsEnabled = true,
                StorageEnabled = true,
                CachingEnabled = true,
                ImportsExportsEnabled = true,
                WebhooksEnabled = true
            }, CancellationToken.None);
            Assert.True(generated.Success, generated.ErrorMessage);

            var integrations = Path.Combine(output, "src", "IntegrationApp.Api", "Integrations");
            Assert.True(File.Exists(Path.Combine(integrations, "Notifications.cs")));
            Assert.True(File.Exists(Path.Combine(integrations, "Storage.cs")));
            Assert.True(File.Exists(Path.Combine(integrations, "Caching.cs")));
            Assert.True(File.Exists(Path.Combine(integrations, "ImportsExports.cs")));
            Assert.True(File.Exists(Path.Combine(integrations, "Webhooks.cs")));
            var config = await File.ReadAllTextAsync(Path.Combine(output, "dotisan.config"));
            Assert.Contains("notifications: enabled", config);
            Assert.Contains("webhooks: enabled", config);
            Assert.Contains("api_port:", config);
            Assert.Contains("web_port:", config);
            Assert.Contains("\"FrontendUrl\": \"http://localhost:", await File.ReadAllTextAsync(Path.Combine(output, "src", "IntegrationApp.Api", "appsettings.json")));
            var notifications = await File.ReadAllTextAsync(Path.Combine(integrations, "Notifications.cs"));
            Assert.Contains("EntityTypeBuilder<NotificationRecord>", notifications);
            Assert.Contains("RequireAntiforgeryTokenAttribute", notifications);
            Assert.Contains("IFileStorage", await File.ReadAllTextAsync(Path.Combine(integrations, "Storage.cs")));
            Assert.Contains("LocalFileStorage", await File.ReadAllTextAsync(Path.Combine(integrations, "Storage.cs")));
            var caching = await File.ReadAllTextAsync(Path.Combine(integrations, "Caching.cs"));
            Assert.Contains("IDistributedApplicationCache", caching);
            Assert.Contains("LoggerMessage", caching);
            Assert.Contains("IMessageBus", await File.ReadAllTextAsync(Path.Combine(integrations, "ImportsExports.cs")));
            Assert.Contains("HMACSHA256", await File.ReadAllTextAsync(Path.Combine(integrations, "Webhooks.cs")));
            Assert.Contains("RetryCount", await File.ReadAllTextAsync(Path.Combine(integrations, "Webhooks.cs")));
            Assert.Contains("ImportStatus", await File.ReadAllTextAsync(Path.Combine(integrations, "ImportsExports.cs")));
            Assert.Contains("GetStatusAsync", await File.ReadAllTextAsync(Path.Combine(integrations, "ImportsExports.cs")));
            Assert.DoesNotContain("S3CompatibleFileStorage", await File.ReadAllTextAsync(Path.Combine(integrations, "Storage.cs")));
            Assert.True(File.Exists(Path.Combine(output, "src", "IntegrationApp.Web", "src", "pages", "NotificationsPage.vue")));
            Assert.True(File.Exists(Path.Combine(output, "src", "IntegrationApp.Web", "src", "pages", "ImportsExportsPage.vue")));
            Assert.True(File.Exists(Path.Combine(output, "src", "IntegrationApp.Web", "src", "pages", "WebhooksPage.vue")));
            Assert.Contains("ReplayAsync", await File.ReadAllTextAsync(Path.Combine(integrations, "Webhooks.cs")));
            Assert.Contains("PayloadJson", await File.ReadAllTextAsync(Path.Combine(integrations, "Webhooks.cs")));
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task V080_integrations_disabled_emit_no_feature_source_or_frontend()
    {
        var output = Path.Combine(Path.GetTempPath(), "dotisan-v080-disabled-" + Guid.NewGuid().ToString("N"));
        try
        {
            var generated = await new GoldenTemplateGenerator().GenerateAsync(ProjectOptions.Quick("PlainApp", output), CancellationToken.None);
            Assert.True(generated.Success, generated.ErrorMessage);
            Assert.False(Directory.Exists(Path.Combine(output, "src", "PlainApp.Api", "Integrations")));
            Assert.False(File.Exists(Path.Combine(output, "src", "PlainApp.Web", "src", "pages", "NotificationsPage.vue")));
            Assert.DoesNotContain("notifications: enabled", await File.ReadAllTextAsync(Path.Combine(output, "dotisan.config")));
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task Generated_sqlite_connection_is_rooted_to_the_api_content_directory()
    {
        var output = Path.Combine(Path.GetTempPath(), "dotisan-sqlite-path-" + Guid.NewGuid().ToString("N"));
        try
        {
            var generated = await new GoldenTemplateGenerator().GenerateAsync(ProjectOptions.Quick("SqliteApp", output), CancellationToken.None);
            Assert.True(generated.Success, generated.ErrorMessage);
            var program = await File.ReadAllTextAsync(Path.Combine(output, "src", "SqliteApp.Api", "Program.cs"));
            Assert.Contains("Path.Combine(contentRoot, dataSource)", program);
            Assert.Contains("builder.Environment.ContentRootPath", program);
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task Generated_api_contains_aspire_and_complete_opentelemetry_wiring()
    {
        var output = Path.Combine(Path.GetTempPath(), "dotisan-observability-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var generated = await new GoldenTemplateGenerator().GenerateAsync(ProjectOptions.Quick("ObservabilityApp", output), CancellationToken.None);
            Assert.True(generated.Success, generated.ErrorMessage);
            var program = await File.ReadAllTextAsync(Path.Combine(output, "src", "ObservabilityApp.Api", "Program.cs"));
            var compose = await File.ReadAllTextAsync(Path.Combine(output, "compose.yaml"));
            Assert.Contains("AddEntityFrameworkCoreInstrumentation", program);
            Assert.Contains("AddOpenTelemetry(logging", program);
            Assert.Contains("aspire-dashboard", compose);
            Assert.Contains("18888:18888", compose);
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Theory]
    [InlineData(DatabaseProvider.SQLite, "Microsoft.EntityFrameworkCore.Sqlite", "UseSqlite", "Data Source=Data/DataApp.db", "SQLite")]
    [InlineData(DatabaseProvider.SqlServer, "Microsoft.EntityFrameworkCore.SqlServer", "UseSqlServer", "Server=localhost,1433;Database=DataApp;User Id=sa;Password=DotisanDev123!;TrustServerCertificate=True", "SQL Server")]
    [InlineData(DatabaseProvider.PostgreSQL, "Npgsql.EntityFrameworkCore.PostgreSQL", "UseNpgsql", "Host=localhost;Database=dataapp;Username=postgres;Password=postgres", "PostgreSQL")]
    [InlineData(DatabaseProvider.MySQL, "Pomelo.EntityFrameworkCore.MySql", "UseMySql", "Server=localhost;Database=dataapp;User=root;Password=root", "MySQL")]
    public async Task Generator_uses_the_selected_database_provider(
        DatabaseProvider provider,
        string package,
        string registration,
        string connectionString,
        string displayName)
    {
        var output = Path.Combine(Path.GetTempPath(), "dotisan-database-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = await new GoldenTemplateGenerator().GenerateAsync(
                ProjectOptions.Quick("DataApp", output) with { Database = provider }, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            var apiProject = await File.ReadAllTextAsync(Path.Combine(output, "src", "DataApp.Api", "DataApp.Api.csproj"));
            var program = await File.ReadAllTextAsync(Path.Combine(output, "src", "DataApp.Api", "Program.cs"));
            var appsettings = await File.ReadAllTextAsync(Path.Combine(output, "src", "DataApp.Api", "appsettings.json"));
            var readme = await File.ReadAllTextAsync(Path.Combine(output, "README.md"));

            Assert.Contains($"<PackageReference Include=\"{package}\"", apiProject);
            Assert.Contains(registration, program);
            Assert.Contains(connectionString, appsettings);
            Assert.Contains($"EF Core {displayName}", readme);
            if (provider == DatabaseProvider.SQLite)
            {
                Assert.Contains("Directory.CreateDirectory", program);
                Assert.Contains("Path.Combine(contentRoot, dataSource)", program);
            }
            if (provider != DatabaseProvider.SQLite)
            {
                Assert.DoesNotContain("Microsoft.EntityFrameworkCore.Sqlite", apiProject);
                var compose = await File.ReadAllTextAsync(Path.Combine(output, "compose.yaml"));
                Assert.Contains("services:", compose);
                Assert.Contains("database:", compose);
                Assert.Contains(provider switch
                {
                    DatabaseProvider.SqlServer => "mcr.microsoft.com/mssql/server:2022-CU16-ubuntu-22.04",
                    DatabaseProvider.PostgreSQL => "postgres:16-alpine",
                    DatabaseProvider.MySQL => "mysql:8.4",
                    _ => throw new InvalidOperationException()
                }, compose);
                Assert.Contains(provider switch
                {
                    DatabaseProvider.SqlServer => "1433:1433",
                    DatabaseProvider.PostgreSQL => "5432:5432",
                    DatabaseProvider.MySQL => "3306:3306",
                    _ => throw new InvalidOperationException()
                }, compose);
                Assert.Contains("healthcheck:", compose);
                if (provider == DatabaseProvider.MySQL)
                    Assert.Contains("mysqladmin ping -h 127.0.0.1 -uroot -proot --silent", compose);
                Assert.Contains("docker compose up -d --wait --wait-timeout 120 database", readme);
            }
            else
            {
                var compose = await File.ReadAllTextAsync(Path.Combine(output, "compose.yaml"));
                Assert.Contains("aspire-dashboard", compose);
            }
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Theory]
    [InlineData(DatabaseProvider.SqlServer)]
    [InlineData(DatabaseProvider.PostgreSQL)]
    [InlineData(DatabaseProvider.MySQL)]
    public async Task Authenticated_provider_projects_keep_sqlite_test_host_support(DatabaseProvider provider)
    {
        var output = Path.Combine(Path.GetTempPath(), "dotisan-auth-database-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = await new GoldenTemplateGenerator().GenerateAsync(
                ProjectOptions.Quick("DataApp", output) with
                {
                    Database = provider,
                    AuthenticationEnabled = true
                }, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            var testProject = await File.ReadAllTextAsync(Path.Combine(output, "tests", "DataApp.Api.Tests", "DataApp.Api.Tests.csproj"));
            Assert.Contains("Microsoft.EntityFrameworkCore.Sqlite", testProject);
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task Resource_scaffolder_creates_vertical_endpoint_model_and_db_registration_without_migration()
    {
        var output = Path.Combine(Path.GetTempPath(), "dotisan-resource-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var generated = await new GoldenTemplateGenerator().GenerateAsync(
                ProjectOptions.Quick("TodoApp", output), CancellationToken.None);
            Assert.True(generated.Success, generated.ErrorMessage);

            var result = await ResourceScaffolder.ScaffoldAsync(output, "Customer", CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            var featureDirectory = Path.Combine(output, "src", "TodoApp.Api", "Features", "Customers");
            Assert.True(File.Exists(Path.Combine(featureDirectory, "Customer.cs")));
            Assert.True(File.Exists(Path.Combine(featureDirectory, "CustomerEndpoints.cs")));
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Api", "Data", "CustomerDbSet.cs")));
            Assert.Contains("MapCustomerEndpoints", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "Infrastructure", "DotisanEndpointExtensions.cs")));
            Assert.DoesNotContain("RequireAuthorization", await File.ReadAllTextAsync(Path.Combine(featureDirectory, "CustomerEndpoints.cs")));
            Assert.DoesNotContain("IAuditWriter", await File.ReadAllTextAsync(Path.Combine(featureDirectory, "CustomerEndpoints.cs")));
            Assert.False(File.Exists(Path.Combine(output, "src", "TodoApp.Api", "Migrations", "CustomerMigration.cs")));
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Crud_scaffolder_creates_editable_frontend_files_outside_generated_output()
    {
        var output = Path.Combine(Path.GetTempPath(), "dotisan-crud-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var generated = await new GoldenTemplateGenerator().GenerateAsync(ProjectOptions.Quick("TodoApp", output), CancellationToken.None);
            Assert.True(generated.Success, generated.ErrorMessage);

            var result = await CrudScaffolder.ScaffoldAsync(output, "Customer", CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Web", "src", "features", "Customers", "CustomerList.vue")));
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Web", "src", "features", "Customers", "CustomerDetail.vue")));
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Web", "src", "features", "Customers", "CustomerForm.vue")));
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Web", "src", "routes", "customers.ts")));
            var routeRegistry = await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Web", "src", "routes", "index.ts"));
            Assert.Contains("customersRoutes", routeRegistry);
            Assert.Contains("routes.push(...customersRoutes)", routeRegistry);
            var listPage = await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Web", "src", "features", "Customers", "CustomerList.vue"));
            Assert.Contains("Delete", listPage);
            Assert.Contains("Loading", listPage);
            Assert.Contains("No customers yet", listPage);
            Assert.All(result.CreatedFiles, file => Assert.DoesNotContain("generated", file, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task Authenticated_resource_scaffolder_generates_crud_permissions_and_policies()
    {
        var output = Path.Combine(Path.GetTempPath(), "dotisan-auth-resource-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var generated = await new GoldenTemplateGenerator().GenerateAsync(
                ProjectOptions.Quick("AuthApp", output) with { AuthenticationEnabled = true }, CancellationToken.None);
            Assert.True(generated.Success, generated.ErrorMessage);

            var result = await ResourceScaffolder.ScaffoldAsync(output, "Customer", CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            var permissions = await File.ReadAllTextAsync(Path.Combine(output, "src", "AuthApp.Api", "Authorization", "Permissions.cs"));
            var endpoints = await File.ReadAllTextAsync(Path.Combine(output, "src", "AuthApp.Api", "Features", "Customers", "CustomerEndpoints.cs"));
            Assert.Contains("CustomersView = \"customers.view\"", permissions);
            Assert.Contains("CustomersCreate = \"customers.create\"", permissions);
            Assert.Contains("CustomersUpdate = \"customers.update\"", permissions);
            Assert.Contains("CustomersDelete = \"customers.delete\"", permissions);
            Assert.Contains("Permissions.CustomersView", endpoints);
            Assert.Contains("Permissions.CustomersCreate", endpoints);
            Assert.Contains("Permissions.CustomersUpdate", endpoints);
            Assert.Contains("Permissions.CustomersDelete", endpoints);
            Assert.Contains("MapPut", endpoints);
            Assert.Contains("MapDelete", endpoints);
            Assert.Contains("IAuditWriter", endpoints);
            Assert.Contains("HttpContext", endpoints);
            Assert.Contains("RecordAsync", endpoints);
            Assert.Contains("\"list\"", endpoints);
            Assert.Contains("\"read\"", endpoints);
            Assert.Contains("\"create\"", endpoints);
            Assert.Contains("\"update\"", endpoints);
            Assert.Contains("\"delete\"", endpoints);

            var secondResult = await ResourceScaffolder.ScaffoldAsync(output, "Order", CancellationToken.None);
            Assert.True(secondResult.Success, secondResult.ErrorMessage);
            permissions = await File.ReadAllTextAsync(Path.Combine(output, "src", "AuthApp.Api", "Authorization", "Permissions.cs"));
            Assert.Contains("[ProfileView, AuthorizationManage, CustomersView, CustomersCreate, CustomersUpdate, CustomersDelete, OrdersView, OrdersCreate, OrdersUpdate, OrdersDelete]", permissions);
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task Authentication_dependencies_are_opt_in()
    {
        var authenticatedOutput = Path.Combine(Path.GetTempPath(), "dotisan-auth-test-" + Guid.NewGuid().ToString("N"));
        var plainOutput = Path.Combine(Path.GetTempPath(), "dotisan-plain-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var authenticated = await new GoldenTemplateGenerator().GenerateAsync(
                ProjectOptions.Quick("AuthApp", authenticatedOutput) with { AuthenticationEnabled = true }, CancellationToken.None);
            var plain = await new GoldenTemplateGenerator().GenerateAsync(
                ProjectOptions.Quick("PlainApp", plainOutput), CancellationToken.None);

            Assert.True(authenticated.Success, authenticated.ErrorMessage);
            Assert.True(plain.Success, plain.ErrorMessage);
            var authServices = await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "src", "AuthApp.Web", "src", "dotisan", "services.ts"));
            Assert.Contains("login", authServices);
            Assert.Contains("register", authServices);
            Assert.Contains("logout", authServices);
            Assert.Contains("me", authServices);
            Assert.Contains("correlationId?: string", authServices);
            Assert.Contains("readonly correlationId", authServices);
            Assert.DoesNotContain("login", await File.ReadAllTextAsync(Path.Combine(plainOutput, "src", "PlainApp.Web", "src", "dotisan", "services.ts")));
            var apiProject = await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "src", "AuthApp.Api", "AuthApp.Api.csproj"));
            var testProject = await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "tests", "AuthApp.Api.Tests", "AuthApp.Api.Tests.csproj"));
            var plainApiProject = await File.ReadAllTextAsync(Path.Combine(plainOutput, "src", "PlainApp.Api", "PlainApp.Api.csproj"));
            var authenticatedPaths = Directory.GetFiles(authenticatedOutput, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(authenticatedOutput, path).Replace(Path.DirectorySeparatorChar, '/'))
                .ToArray();
            var plainPaths = Directory.GetFiles(plainOutput, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(plainOutput, path).Replace(Path.DirectorySeparatorChar, '/'))
                .ToArray();
            var dbContext = await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "src", "AuthApp.Api", "Data", "AppDbContext.cs"));
            var user = await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "src", "AuthApp.Api", "Identity", "ApplicationUser.cs"));
            var authenticatedProgram = await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "src", "AuthApp.Api", "Program.cs"));
            var productionConfiguration = await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "src", "AuthApp.Api", "Infrastructure", "DotisanProductionConfiguration.cs"));
            Assert.Contains("X-Correlation-ID", authenticatedProgram);
            Assert.Contains("CustomizeProblemDetails", authenticatedProgram);
            Assert.Contains("DotisanProductionConfiguration.Validate", authenticatedProgram);
            Assert.Contains("Dotisan:Security:FrontendUrl must be an absolute HTTPS URL outside Development", productionConfiguration);
            Assert.Contains("Dotisan:Security:DataProtectionKeyDirectory is required outside Development", productionConfiguration);
            Assert.Contains("Mail:Provider must be smtp or a custom provider outside Development", authenticatedProgram);
            Assert.Contains("UseForwardedHeaders", authenticatedProgram);
            Assert.Contains("KnownProxies", authenticatedProgram);
            Assert.Contains("CookieSecurePolicy.Always", authenticatedProgram);
            Assert.Contains("WithOrigins", authenticatedProgram);
            Assert.Contains("DotisanSecurityOptions", authenticatedProgram);
            Assert.Contains("ValidateOnStart", authenticatedProgram);
            Assert.DoesNotContain("Database.Migrate()", authenticatedProgram);
            Assert.Contains("/health/live", await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "src", "AuthApp.Api", "Features", "Health", "HealthEndpoints.cs")));
            Assert.Contains("/health/ready", await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "src", "AuthApp.Api", "Features", "Health", "HealthEndpoints.cs")));
            var plainProgram = await File.ReadAllTextAsync(Path.Combine(plainOutput, "src", "PlainApp.Api", "Program.cs"));
            var authenticatedEndpointRegistry = await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "src", "AuthApp.Api", "Infrastructure", "DotisanEndpointExtensions.cs"));
            var plainEndpointRegistry = await File.ReadAllTextAsync(Path.Combine(plainOutput, "src", "PlainApp.Api", "Infrastructure", "DotisanEndpointExtensions.cs"));
            var authenticatedReadme = await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "README.md"));

            Assert.Contains("Microsoft.AspNetCore.Identity.EntityFrameworkCore", apiProject);
            Assert.Contains("Microsoft.AspNetCore.Mvc.Testing", testProject);
            Assert.Contains("Microsoft.Data.Sqlite", testProject);
            Assert.DoesNotContain("Microsoft.AspNetCore.Identity.EntityFrameworkCore", plainApiProject);
            Assert.Contains("src/AuthApp.Api/Identity/ApplicationUser.cs", authenticatedPaths);
            Assert.Contains("IdentityDbContext<ApplicationUser>", dbContext);
            Assert.Contains("public sealed partial class AppDbContext", dbContext);
            Assert.Contains("public sealed class ApplicationUser : IdentityUser", user);
            Assert.DoesNotContain("src/PlainApp.Api/Identity/ApplicationUser.cs", plainPaths);
            Assert.Contains("src/AuthApp.Api/Features/Account/AccountEndpoints.cs", authenticatedPaths);
            Assert.Contains("src/AuthApp.Api/Features/Authorization/AuthorizationEndpoints.cs", authenticatedPaths);
            Assert.DoesNotContain("src/PlainApp.Api/Features/Authorization/AuthorizationEndpoints.cs", plainPaths);
            var authorizationEndpoints = await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "src", "AuthApp.Api", "Features", "Authorization", "AuthorizationEndpoints.cs"));
            Assert.Contains("MapGet(\"/api/authorization/profile\"", authorizationEndpoints);
            Assert.Contains("RequireAuthorization(Permissions.ProfileView)", authorizationEndpoints);
            Assert.Contains("src/AuthApp.Api/Authorization/Permissions.cs", authenticatedPaths);
            Assert.Contains("src/AuthApp.Api/Auditing/AuditEntry.cs", authenticatedPaths);
            Assert.Contains("src/AuthApp.Api/Auditing/IAuditWriter.cs", authenticatedPaths);
            Assert.Contains("src/AuthApp.Api/Auditing/AuditWriter.cs", authenticatedPaths);
            Assert.DoesNotContain("src/PlainApp.Api/Authorization/Permissions.cs", plainPaths);
            var permissions = await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "src", "AuthApp.Api", "Authorization", "Permissions.cs"));
            Assert.Contains("public const string ClaimType = \"permission\";", permissions);
            Assert.Contains("public const string ProfileView = \"profile.view\";", permissions);
            Assert.Contains("IReadOnlyList<string> All", permissions);
            var accountEndpoints = await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "src", "AuthApp.Api", "Features", "Account", "AccountEndpoints.cs"));
            Assert.Contains("MapAccountEndpoints", accountEndpoints);
            Assert.Contains("RegisterRequest", accountEndpoints);
            Assert.Contains("LoginRequest", accountEndpoints);
            Assert.Contains("CurrentUserResponse", accountEndpoints);
            Assert.Contains("PasswordSignInAsync", accountEndpoints);
            Assert.Contains("UserManager<ApplicationUser>", accountEndpoints);
            Assert.Contains("AuthenticationScheme = IdentityConstants.ApplicationScheme", accountEndpoints);
            Assert.Contains("IAuditWriter", accountEndpoints);
            Assert.Contains("security.registered", accountEndpoints);
            Assert.Contains("security.registration.denied", accountEndpoints);
            Assert.Contains("security.login.succeeded", accountEndpoints);
            Assert.Contains("security.login.failed", accountEndpoints);
            Assert.Contains("security.logout", accountEndpoints);
            Assert.Contains("PasswordResetRequest", accountEndpoints);
            Assert.Contains("GeneratePasswordResetTokenAsync", accountEndpoints);
            Assert.Contains("EmailConfirmationRequest", accountEndpoints);
            Assert.Contains("EmailConfirmationResendRequest", accountEndpoints);
            Assert.Contains("GenerateEmailConfirmationTokenAsync", accountEndpoints);
            Assert.Contains("IExternalLoginProvider", await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "src", "AuthApp.Api", "Integrations", "IExternalLoginProvider.cs")));
            Assert.Contains("SendGridEmailProvider", await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "src", "AuthApp.Api", "Integrations", "IntegrationExamples.cs")));
            Assert.Contains("IEmailProvider", accountEndpoints);
            Assert.Contains("public const string AuthorizationManage", permissions);
            Assert.Contains("MapGet(\"/api/authorization/users\"", authorizationEndpoints);
            Assert.Contains("MapPost(\"/api/authorization/users/{userId}/roles/{roleName}\"", authorizationEndpoints);
            Assert.True(File.Exists(Path.Combine(authenticatedOutput, "src", "AuthApp.Web", "src", "pages", "admin", "AuthorizationPage.vue")));
            var authRoutes = await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "src", "AuthApp.Web", "src", "routes", "index.ts"));
            Assert.Contains("AuthorizationPage", authRoutes);
            Assert.Contains("requiresPermission", authRoutes);
            Assert.Contains("password-reset/request", authServices);
            Assert.Contains("email-confirmation/confirm", authServices);
            Assert.True(File.Exists(Path.Combine(authenticatedOutput, "src", "AuthApp.Web", "src", "pages", "auth", "EmailConfirmationPage.vue")));
            var confirmationPage = await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "src", "AuthApp.Web", "src", "pages", "auth", "EmailConfirmationPage.vue"));
            Assert.Contains("if (!route.query.token)", confirmationPage);
            Assert.Contains("Check your email", confirmationPage);
            Assert.Contains("email-confirmation/resend", authServices);
            Assert.Contains("Connect", await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "src", "AuthApp.Web", "src", "pages", "account", "ExternalLoginsPage.vue")));
            Assert.DoesNotContain("IntegrationExamples.cs", plainPaths);
            Assert.Contains("/mfa/setup", accountEndpoints);
            Assert.Contains("GetAuthenticatorKeyAsync", accountEndpoints);
            Assert.Contains("TwoFactorAuthenticatorSignInAsync", accountEndpoints);
            Assert.Contains("mfa_required", accountEndpoints);
            Assert.Contains("LoginResponse", accountEndpoints);
            Assert.Contains("GenerateNewTwoFactorRecoveryCodesAsync", accountEndpoints);
            Assert.True(File.Exists(Path.Combine(authenticatedOutput, "src", "AuthApp.Web", "src", "pages", "account", "MfaPage.vue")));
            Assert.True(File.Exists(Path.Combine(authenticatedOutput, "src", "AuthApp.Web", "src", "pages", "auth", "MfaChallengePage.vue")));
            Assert.DoesNotContain("AccountEndpoints.cs", plainPaths);
            Assert.DoesNotContain("AuthorizationPage.vue", plainPaths);
            Assert.Contains("AddIdentityCore<ApplicationUser>", authenticatedProgram);
            Assert.Contains("AddRoles<IdentityRole>()", authenticatedProgram);
            Assert.Contains("AddEntityFrameworkStores<AppDbContext>", authenticatedProgram);
            Assert.Contains("AddAuthentication(IdentityConstants.ApplicationScheme)", authenticatedProgram);
            Assert.Contains("AddRateLimiter", authenticatedProgram);
            Assert.Contains("RequireRateLimiting(\"account\")", accountEndpoints);
            Assert.Contains("TwoFactorRecoveryCodeSignInAsync", accountEndpoints);
            Assert.Contains("AddAuthorization(options =>", authenticatedProgram);
            Assert.Contains("AddScoped<IAuditWriter, AuditWriter>", authenticatedProgram);
            Assert.Contains("Permissions.All", authenticatedProgram);
            Assert.Contains("RequireClaim(Permissions.ClaimType, permission)", authenticatedProgram);
            Assert.Contains("UseAuthentication", authenticatedProgram);
            Assert.Contains("UseAuthorization", authenticatedProgram);
            Assert.Contains("UseAntiforgery", authenticatedProgram);
            Assert.DoesNotContain("MapAccountEndpoints", authenticatedProgram);
            Assert.DoesNotContain("MapAuthorizationEndpoints", authenticatedProgram);
            Assert.Contains("AccountEndpoints.MapAccountEndpoints", authenticatedEndpointRegistry);
            Assert.Contains("AuthorizationEndpoints.MapAuthorizationEndpoints", authenticatedEndpointRegistry);
            Assert.Contains("HealthEndpoints.MapHealthEndpoints", authenticatedEndpointRegistry);
            Assert.Contains("JobEndpoints.MapJobEndpoints", authenticatedEndpointRegistry);
            Assert.Contains("HealthEndpoints.MapHealthEndpoints", plainEndpointRegistry);
            Assert.Contains("JobEndpoints.MapJobEndpoints", plainEndpointRegistry);
            Assert.Contains("using Microsoft.AspNetCore.Identity;", authenticatedProgram);
            Assert.DoesNotContain("using Dotisan.Core;", authenticatedProgram);
            Assert.DoesNotContain("using Dotisan.Core;", accountEndpoints);
            Assert.Contains("RequireAntiforgeryTokenAttribute", accountEndpoints);
            Assert.DoesNotContain("RequireAntiforgery()", accountEndpoints);
            Assert.DoesNotContain("AddIdentityCore<ApplicationUser>", plainProgram);
            Assert.DoesNotContain("MapAccountEndpoints", plainProgram);
            Assert.Contains("tests/AuthApp.Api.Tests/AuthenticationEndpointTests.cs", authenticatedPaths);
            var authenticationTests = await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "tests", "AuthApp.Api.Tests", "AuthenticationEndpointTests.cs"));
            Assert.Contains("WebApplicationFactory<Program>", authenticationTests);
            Assert.Contains("UseEphemeralDataProtectionProvider", authenticationTests);
            Assert.Contains("RoleManager<IdentityRole>", authenticationTests);
            Assert.Contains("AddClaimAsync", authenticationTests);
            Assert.Contains("Permissions.ProfileView", authenticationTests);
            Assert.Contains("HttpStatusCode.Forbidden", authenticationTests);
            Assert.Contains("/api/authorization/profile", authenticationTests);
            Assert.Contains("ReadAuditEntriesAsync", authenticationTests);
            Assert.Contains("security.login.succeeded", authenticationTests);
            Assert.Contains("X-Correlation-ID", authenticationTests);
            Assert.Contains("TraceId", authenticationTests);
            Assert.Contains("Audit:Enabled", authenticationTests);
            Assert.Contains("--auth yes", authenticatedReadme);
            Assert.Contains("InitialIdentity", authenticatedReadme);
            Assert.Contains("dotisan migrate", authenticatedReadme);
            Assert.Contains("does not create or apply migrations", authenticatedReadme);
            Assert.Contains("RequireAuthorization", authenticatedReadme);
            Assert.Contains("RoleManager<IdentityRole>", authenticatedReadme);
            Assert.Contains("403", authenticatedReadme);
            Assert.Contains("IAuditWriter", authenticatedReadme);
            Assert.Contains("Audit__Enabled", authenticatedReadme);
            Assert.Contains("not event sourcing", authenticatedReadme);
            Assert.Contains("checks the .NET SDK", authenticatedReadme);
            Assert.Contains("Press Ctrl+C once", authenticatedReadme);
        }
        finally
        {
            if (Directory.Exists(authenticatedOutput))
                Directory.Delete(authenticatedOutput, recursive: true);
            if (Directory.Exists(plainOutput))
                Directory.Delete(plainOutput, recursive: true);
        }
    }

    [Fact]
    public async Task Generation_is_reproducible_for_the_same_feature_matrix()
    {
        var first = Path.Combine(Path.GetTempPath(), "dotisan-repro-first-" + Guid.NewGuid().ToString("N"));
        var second = Path.Combine(Path.GetTempPath(), "dotisan-repro-second-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = ProjectOptions.Quick("ReproApp", first) with
            {
                AuthenticationEnabled = true,
                Registration = RegistrationPolicy.Public,
                MultiTenancyEnabled = true,
                NotificationsEnabled = true,
                StorageEnabled = true,
                CachingEnabled = true,
                ImportsExportsEnabled = true,
                WebhooksEnabled = true,
                MailProvider = MailProvider.Mailpit,
                PackageManager = PackageManager.Npm
            };
            Assert.True((await new GoldenTemplateGenerator().GenerateAsync(options, CancellationToken.None)).Success);
            Assert.True((await new GoldenTemplateGenerator().GenerateAsync(options with { OutputDirectory = second }, CancellationToken.None)).Success);

            var firstFiles = Directory.GetFiles(first, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(first, path).Replace(Path.DirectorySeparatorChar, '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var secondFiles = Directory.GetFiles(second, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(second, path).Replace(Path.DirectorySeparatorChar, '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(firstFiles, secondFiles);
            foreach (var relativePath in firstFiles)
            {
                var firstHash = await HashAsync(Path.Combine(first, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                var secondHash = await HashAsync(Path.Combine(second, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                Assert.Equal(firstHash, secondHash);
            }
        }
        finally
        {
            if (Directory.Exists(first)) Directory.Delete(first, recursive: true);
            if (Directory.Exists(second)) Directory.Delete(second, recursive: true);
        }
    }

    private static async Task<string> HashAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(stream));
    }
}
