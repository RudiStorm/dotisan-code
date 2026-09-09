using Dotisan.Core;
using System.Reflection;

namespace Dotisan.Generators;

internal sealed record TemplateFile(string Path, string Content);

internal static class TemplateFiles
{
    public static IReadOnlyList<TemplateFile> Create(ProjectOptions options, string identifier)
    {
        var packageManager = options.PackageManager == PackageManager.Pnpm ? "pnpm@9.15.0" : "npm@10.9.0";
        return
        [
            new(".gitignore", TemplateRenderer.Render(TemplateCatalog.Select("static/gitignore.template"), TemplateContext.Empty)),
            new("global.json", TemplateRenderer.Render(TemplateCatalog.Select("static/global.json.template"), TemplateContext.Empty)),
            new(".node-version", TemplateRenderer.Render(TemplateCatalog.Select("static/node-version.template"), TemplateContext.Empty)),
            new("Directory.Packages.props", TemplateRenderer.Render(TemplateCatalog.Select("static/directory-packages.props.template"), TemplateContext.Empty)),
            new("README.md", TemplateRenderer.Render(TemplateCatalog.Select(options.AuthenticationEnabled ? "static/readme-authenticated.template" : "static/readme-plain.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["PROJECT_NAME"] = options.Name,
                ["DATABASE_DISPLAY"] = DatabaseDisplayName(options.Database),
                ["DATABASE_SETUP"] = DatabaseSetup(options.Name, options.Database)
            }))),
            new("PRODUCT.md", TemplateRenderer.Render(TemplateCatalog.Select("static/product.template"), TemplateContext.Empty)),
            new("DESIGN.md", TemplateRenderer.Render(TemplateCatalog.Select("static/design.template"), TemplateContext.Empty)),
            new("dotisan.config", TemplateRenderer.Render(TemplateCatalog.Select("static/dotisan-config.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["JOBS"] = options.JobsEnabled ? "wolverine" : "none",
                ["DATABASE"] = options.Database.ToString().ToLowerInvariant(),
                ["AUTHENTICATION"] = options.AuthenticationEnabled ? "enabled" : "disabled",
                ["MULTI_TENANCY"] = options.MultiTenancyEnabled ? "enabled" : "disabled",
                ["API_PORT"] = DevelopmentApiPort(options.Name).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["WEB_PORT"] = DevelopmentWebPort(options.Name).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["MAIL_PROVIDER"] = options.MailProvider.ToString().ToLowerInvariant(),
                ["NOTIFICATIONS"] = options.NotificationsEnabled ? "enabled" : "disabled",
                ["STORAGE"] = options.StorageEnabled ? "enabled" : "disabled",
                ["CACHING"] = options.CachingEnabled ? "enabled" : "disabled",
                ["IMPORTS_EXPORTS"] = options.ImportsExportsEnabled ? "enabled" : "disabled",
                ["WEBHOOKS"] = options.WebhooksEnabled ? "enabled" : "disabled",
                ["PACKAGE_MANAGER"] = options.PackageManager.ToString().ToLowerInvariant(),
                ["MAIL_SERVICE"] = options.MailProvider == MailProvider.Mailpit ? "true" : "false"
                ,["OBSERVABILITY"] = options.ObservabilityEnabled ? "true" : "false"
            }))),
            new("dotisan.contract.json", InitialContractManifest(options.AuthenticationEnabled).ToJson()),
            new("Dockerfile", TemplateRenderer.Render(TemplateCatalog.Select(options.PackageManager == PackageManager.Pnpm ? "static/dockerfile-pnpm.template" : "static/dockerfile-npm.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["PROJECT_NAME"] = options.Name
            }))),
            new TemplateFile("compose.yaml", DatabaseCompose(options.Name, options.Database, options.MailProvider)),
            new($"src/{options.Name}.Api/{options.Name}.Api.csproj", TemplateRenderer.Render(TemplateCatalog.Select("static/api-project.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["ROOT_NAMESPACE"] = options.Name.Replace('-', '_'),
                ["DATABASE_PACKAGE"] = DatabasePackage(options.Database),
                ["JOBS_PACKAGES"] = options.JobsEnabled ? $"    <PackageReference Include=\"WolverineFx\" />\n    <PackageReference Include=\"WolverineFx.EntityFrameworkCore\" />\n    <PackageReference Include=\"WolverineFx.RuntimeCompilation\" />\n    <PackageReference Include=\"WolverineFx.{WolverineProviderPackage(options.Database)}\" />" : string.Empty,
                ["AUTH_PACKAGE"] = options.AuthenticationEnabled ? "    <PackageReference Include=\"Microsoft.AspNetCore.Identity.EntityFrameworkCore\" />" : string.Empty
                ,["OTEL_PACKAGES"] = options.ObservabilityEnabled ? "    <PackageReference Include=\"OpenTelemetry.Extensions.Hosting\" />\n    <PackageReference Include=\"OpenTelemetry.Instrumentation.AspNetCore\" />\n    <PackageReference Include=\"OpenTelemetry.Instrumentation.EntityFrameworkCore\" />\n    <PackageReference Include=\"OpenTelemetry.Instrumentation.Http\" />\n    <PackageReference Include=\"OpenTelemetry.Exporter.OpenTelemetryProtocol\" />" : string.Empty
            }))),
            new($"src/{options.Name}.Api/Program.cs", TemplateRenderer.Render(TemplateCatalog.Select(options.AuthenticationEnabled ? "static/program-auth.template" : "static/program-plain.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["IDENTIFIER"] = identifier
            }))),
            new($"src/{options.Name}.Api/Infrastructure/ServiceCollectionExtensions.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/service-collection-extensions.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["IDENTIFIER"] = identifier,
                ["AUTH_USING"] = options.AuthenticationEnabled ? $"using {identifier}.Api.Authorization;\nusing Microsoft.AspNetCore.Authentication.Cookies;\nusing Microsoft.AspNetCore.Identity;\nusing Microsoft.AspNetCore.RateLimiting;\nusing System.Threading.RateLimiting;" : string.Empty,
                ["IDENTITY_USING"] = options.AuthenticationEnabled ? $"using {identifier}.Api.Identity;" : string.Empty,
                ["TENANT_USING"] = options.MultiTenancyEnabled ? $"using {identifier}.Api.Tenancy;" : string.Empty,
                ["INTEGRATIONS_USING"] = options.AuthenticationEnabled || options.NotificationsEnabled || options.StorageEnabled || options.CachingEnabled || options.ImportsExportsEnabled || options.WebhooksEnabled ? $"using {identifier}.Api.Integrations;" : string.Empty,
                ["JOBS_USING"] = options.JobsEnabled ? $"using {identifier}.Api.Jobs;" : string.Empty,
                ["WOLVERINE_USING"] = options.JobsEnabled ? "using Wolverine;" : string.Empty,
                ["OTEL_USINGS"] = options.ObservabilityEnabled ? "using OpenTelemetry;\nusing OpenTelemetry.Metrics;\nusing OpenTelemetry.Trace;\nusing OpenTelemetry.Logs;" : string.Empty,
                ["OTEL_SERVICES"] = ObservabilityServices(options.ObservabilityEnabled),
                ["DATABASE_REGISTRATION"] = DatabaseRegistration(options.Database),
                ["EMAIL_CONFIRMATION_ENABLED"] = options.AuthenticationEnabled ? "true" : "false",
                ["DATA_PROTECTION_ENABLED"] = options.AuthenticationEnabled ? "true" : "false",
                ["NOTIFICATIONS_SERVICES"] = options.NotificationsEnabled ? "        builder.Services.AddSignalR();\n        builder.Services.AddSingleton<IUserIdProvider, NotificationUserIdProvider>();\n        builder.Services.AddScoped<INotificationStore, EfNotificationStore>();" : string.Empty,
                ["STORAGE_SERVICES"] = options.StorageEnabled ? "        builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();" : string.Empty,
                ["CACHING_SERVICES"] = options.CachingEnabled ? "        builder.Services.AddMemoryCache();\n        builder.Services.AddSingleton<IDistributedApplicationCache, MemoryApplicationCache>();\n        builder.Services.AddSingleton<IApplicationCache>(services => services.GetRequiredService<IDistributedApplicationCache>());" : string.Empty,
                ["IMPORTS_SERVICES"] = options.ImportsExportsEnabled ? "        builder.Services.AddScoped<IDataExchangeService, DataExchangeService>();" : string.Empty,
                ["WEBHOOK_SERVICES"] = options.WebhooksEnabled ? $"        builder.Services.AddHttpClient(\"webhooks\", client => client.Timeout = TimeSpan.FromSeconds(30));\n        builder.Services.AddScoped<IWebhookDispatcher, {(options.JobsEnabled ? "DurableWebhookDispatcher" : "HmacWebhookDispatcher")}>();" : string.Empty,
                ["TENANT_SERVICES"] = options.MultiTenancyEnabled ? "        builder.Services.AddHttpContextAccessor();\n        builder.Services.AddScoped<ITenantContext, TenantContext>();" : string.Empty,
                ["JOBS_SERVICES"] = options.JobsEnabled ? "        builder.Host.UseWolverine(opts => JobRegistration.Configure(opts, connectionString, builder.Configuration));" : string.Empty,
                ["AUTH_SERVICES"] = options.AuthenticationEnabled ? AuthenticationServices() : string.Empty
            }))),
            new($"src/{options.Name}.Api/Data/AppDbContext.cs", TemplateRenderer.Render(TemplateCatalog.Select(options.AuthenticationEnabled ? "static/db-context-identity.template" : "static/db-context-plain.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["IDENTIFIER"] = identifier,
                ["INTEGRATION_USING"] = options.NotificationsEnabled || options.WebhooksEnabled || options.ImportsExportsEnabled ? $"using {identifier}.Api.Integrations;\n" : string.Empty,
                ["DBSETS"] = string.Concat(options.NotificationsEnabled ? "    public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();\n" : string.Empty, options.WebhooksEnabled ? "    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();\n" : string.Empty, options.ImportsExportsEnabled ? "    public DbSet<ImportRecord> ImportRecords => Set<ImportRecord>();\n" : string.Empty)
            }))),
            new($"src/{options.Name}.Api/Auditing/AuditEntry.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/audit-entry.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["IDENTIFIER"] = identifier
            }))),
            new($"src/{options.Name}.Api/Auditing/IAuditWriter.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/audit-writer-contract.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["IDENTIFIER"] = identifier
            }))),
            new($"src/{options.Name}.Api/Auditing/AuditWriter.cs", TemplateRenderer.Render(TemplateCatalog.Select(options.MultiTenancyEnabled ? "static/audit-writer-tenant.template" : "static/audit-writer.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["IDENTIFIER"] = identifier
            }))),
            ..(options.MultiTenancyEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Tenancy/TenantContext.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/tenant-context.template"), new TemplateContext(new Dictionary<string, string>
                    {
                        ["IDENTIFIER"] = identifier
                    }))) }
                : Array.Empty<TemplateFile>()),
            ..(options.JobsEnabled ? new[] {
                new TemplateFile($"src/{options.Name}.Api/Jobs/JobRegistration.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/job-registration.template"), new TemplateContext(new Dictionary<string, string> { ["IDENTIFIER"] = identifier, ["WOLVERINE_PROVIDER"] = WolverineProviderPackage(options.Database), ["PERSISTENCE_METHOD"] = WolverinePersistenceMethod(options.Database) }))),
                new TemplateFile($"src/{options.Name}.Api/Jobs/SampleJob.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/sample-job.template"), new TemplateContext(new Dictionary<string, string> { ["IDENTIFIER"] = identifier }))),
                new TemplateFile($"src/{options.Name}.Api/Jobs/SampleJobHandler.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/sample-job-handler.template"), new TemplateContext(new Dictionary<string, string> { ["IDENTIFIER"] = identifier }))),
                new TemplateFile($"src/{options.Name}.Api/Features/Jobs/JobEndpoints.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/job-endpoints.template"), new TemplateContext(new Dictionary<string, string> { ["IDENTIFIER"] = identifier, ["ANTIFORGERY_USING"] = options.AuthenticationEnabled ? "using Microsoft.AspNetCore.Antiforgery;" : string.Empty, ["AUTHORIZATION_USING"] = options.AuthenticationEnabled ? $"using {identifier}.Api.Authorization;" : string.Empty, ["AUTHORIZATION"] = options.AuthenticationEnabled ? ".RequireAuthorization(Permissions.AuthorizationManage)" : string.Empty, ["ANTIFORGERY"] = options.AuthenticationEnabled ? ".WithMetadata(new RequireAntiforgeryTokenAttribute(true))" : string.Empty })))
            } : Array.Empty<TemplateFile>()),
            new($"src/{options.Name}.Api/Features/Health/HealthEndpoints.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/health-endpoints.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["IDENTIFIER"] = identifier
            }))),
            new($"src/{options.Name}.Api/Infrastructure/DotisanSecurityOptions.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/security-options.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["IDENTIFIER"] = identifier
            }))),
            new($"src/{options.Name}.Api/Infrastructure/ApplicationBuilderExtensions.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/application-builder-extensions.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["IDENTIFIER"] = identifier
            }))),
            new($"src/{options.Name}.Api/Infrastructure/DotisanProductionConfiguration.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/production-configuration.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["IDENTIFIER"] = identifier,
                ["EMAIL_CONFIRMATION_ENABLED"] = options.AuthenticationEnabled ? "true" : "false"
            }))),
            ..(options.NotificationsEnabled ? new[] { new TemplateFile($"src/{options.Name}.Api/Integrations/Notifications.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/notifications.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["IDENTIFIER"] = identifier,
                ["AUTH_USING"] = options.AuthenticationEnabled ? $"using {identifier}.Api.Authorization;\nusing Microsoft.AspNetCore.Antiforgery;" : string.Empty,
                ["TENANT_USING"] = options.MultiTenancyEnabled ? $"using {identifier}.Api.Tenancy;" : string.Empty,
                ["AUTHORIZATION"] = options.AuthenticationEnabled ? "group.RequireAuthorization();" : string.Empty,
                ["AUTHORIZATION_POST"] = options.AuthenticationEnabled ? ".RequireAuthorization(Permissions.NotificationsSend).WithMetadata(new RequireAntiforgeryTokenAttribute(true))" : string.Empty,
                ["ANTIFORGERY"] = options.AuthenticationEnabled ? ".WithMetadata(new RequireAntiforgeryTokenAttribute(true))" : string.Empty,
                ["TENANT_PARAMETER"] = options.MultiTenancyEnabled ? ", ITenantContext tenantContext" : string.Empty,
                ["TENANT_VALUE"] = options.MultiTenancyEnabled ? "tenantContext.TenantId" : "null"
            }))) } : Array.Empty<TemplateFile>()),
            ..(options.StorageEnabled ? new[] { new TemplateFile($"src/{options.Name}.Api/Integrations/Storage.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/storage.template"), new TemplateContext(new Dictionary<string, string> { ["IDENTIFIER"] = identifier, ["ANTIFORGERY_USING"] = options.AuthenticationEnabled ? "using Microsoft.AspNetCore.Antiforgery;\nusing {identifier}.Api.Authorization;".Replace("{identifier}", identifier, StringComparison.Ordinal) : string.Empty, ["AUTHORIZATION"] = options.AuthenticationEnabled ? "group.RequireAuthorization();" : string.Empty, ["DELETE_AUTHORIZATION"] = options.AuthenticationEnabled ? ".RequireAuthorization(Permissions.StorageDelete)" : string.Empty, ["ANTIFORGERY"] = options.AuthenticationEnabled ? ".WithMetadata(new RequireAntiforgeryTokenAttribute(true))" : string.Empty }))) } : Array.Empty<TemplateFile>()),
            ..(options.CachingEnabled ? new[] { new TemplateFile($"src/{options.Name}.Api/Integrations/Caching.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/caching.template"), new TemplateContext(new Dictionary<string, string> { ["IDENTIFIER"] = identifier }))) } : Array.Empty<TemplateFile>()),
            ..(options.ImportsExportsEnabled ? new[] { new TemplateFile($"src/{options.Name}.Api/Integrations/ImportsExports.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/imports-exports.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["IDENTIFIER"] = identifier,
                ["AUTH_USING"] = options.AuthenticationEnabled ? $"using Microsoft.AspNetCore.Antiforgery;\nusing {identifier}.Api.Authorization;" : string.Empty,
                ["TENANT_USING"] = options.MultiTenancyEnabled ? $"using {identifier}.Api.Tenancy;" : string.Empty,
                ["AUTHORIZATION"] = options.AuthenticationEnabled ? "group.RequireAuthorization(Permissions.ImportExportManage);" : string.Empty,
                ["ANTIFORGERY"] = options.AuthenticationEnabled ? ".WithMetadata(new RequireAntiforgeryTokenAttribute(true))" : string.Empty,
                ["TENANT_PARAMETER"] = options.MultiTenancyEnabled ? ", ITenantContext tenantContext" : string.Empty,
                ["TENANT_REQUIRE"] = options.MultiTenancyEnabled ? "tenantContext.RequireTenantId()" : "null",
                ["TENANT_QUERY"] = options.MultiTenancyEnabled ? "var tenantId = tenantContext.RequireTenantId(); query = query.Where(item => item.TenantId == tenantId);" : string.Empty
            }))) } : Array.Empty<TemplateFile>()),
            ..(options.WebhooksEnabled ? new[] { new TemplateFile($"src/{options.Name}.Api/Integrations/Webhooks.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/webhooks.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["IDENTIFIER"] = identifier,
                ["AUTH_USING"] = options.AuthenticationEnabled ? $"using {identifier}.Api.Authorization;" : string.Empty,
                ["WOLVERINE_USING"] = options.JobsEnabled ? "using Wolverine;" : string.Empty,
                ["AUTHORIZATION"] = options.AuthenticationEnabled ? "group.RequireAuthorization();" : string.Empty,
                ["AUTHORIZATION_REPLAY"] = options.AuthenticationEnabled ? ".RequireAuthorization(Permissions.WebhookReplay)" : string.Empty,
                ["REPLAY_STATUS"] = options.JobsEnabled ? "\"queued\"" : "\"delivered\"",
                ["DURABLE_SUPPORT"] = options.JobsEnabled ? TemplateRenderer.Render(TemplateCatalog.Select("static/webhooks-durable.template"), new TemplateContext(new Dictionary<string, string> { ["IDENTIFIER"] = identifier })) : string.Empty
            }))) } : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Identity/ApplicationUser.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/application-user.template"), new TemplateContext(new Dictionary<string, string>
                    {
                        ["IDENTIFIER"] = identifier
                    }))) }
                : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Identity/ApplicationSession.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/application-session.template"), new TemplateContext(new Dictionary<string, string>
                    {
                        ["IDENTIFIER"] = identifier
                    }))) }
                : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Integrations/IEmailProvider.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/email-provider.template"), new TemplateContext(new Dictionary<string, string>
                    {
                        ["IDENTIFIER"] = identifier
                    }))) }
                : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Integrations/IExternalLoginProvider.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/external-login-provider.template"), new TemplateContext(new Dictionary<string, string>
                    {
                        ["IDENTIFIER"] = identifier
                    }))) }
                : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Integrations/IntegrationExamples.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/integration-examples.template"), new TemplateContext(new Dictionary<string, string>
                    {
                        ["IDENTIFIER"] = identifier
                    }))) }
                : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Authorization/Permissions.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/permissions.template"), new TemplateContext(new Dictionary<string, string>
                    {
                        ["IDENTIFIER"] = identifier
                    }))) }
                : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Features/Account/AccountEndpoints.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/account-endpoints.template"), new TemplateContext(new Dictionary<string, string>
                    {
                        ["IDENTIFIER"] = identifier
                    }))) }
                : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Api/Features/Authorization/AuthorizationEndpoints.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/authorization-endpoints.template"), new TemplateContext(new Dictionary<string, string>
                    {
                        ["IDENTIFIER"] = identifier
                    }))) }
                : Array.Empty<TemplateFile>()),
            new($"src/{options.Name}.Api/Infrastructure/DotisanEndpointExtensions.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/endpoint-extensions.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["IDENTIFIER"] = identifier,
                ["JOBS_USING"] = options.JobsEnabled ? $"using {identifier}.Api.Features.Jobs;" : string.Empty,
                ["INTEGRATIONS_USING"] = options.NotificationsEnabled || options.StorageEnabled || options.ImportsExportsEnabled || options.WebhooksEnabled ? $"using {identifier}.Api.Integrations;" : string.Empty,
                ["AUTH_USING"] = options.AuthenticationEnabled ? $"using {identifier}.Api.Features.Account;\nusing {identifier}.Api.Features.Authorization;" : string.Empty,
                ["JOB_MAP"] = options.JobsEnabled ? "        JobEndpoints.MapJobEndpoints(endpoints);" : string.Empty,
                ["NOTIFICATIONS_MAP"] = options.NotificationsEnabled ? "        NotificationEndpoints.Map(endpoints); endpoints.MapHub<NotificationHub>(\"/hubs/notifications\");" : string.Empty,
                ["STORAGE_MAP"] = options.StorageEnabled ? "        StorageEndpoints.Map(endpoints);" : string.Empty,
                ["IMPORTS_MAP"] = options.ImportsExportsEnabled ? "        ImportExportEndpoints.Map(endpoints);" : string.Empty,
                ["WEBHOOKS_MAP"] = options.WebhooksEnabled ? "        WebhookEndpoints.Map(endpoints);" : string.Empty,
                ["AUTH_MAP"] = options.AuthenticationEnabled ? $"        AccountEndpoints.MapAccountEndpoints(endpoints, {(options.Registration == RegistrationPolicy.Public ? "true" : "false")});\n        AuthorizationEndpoints.MapAuthorizationEndpoints(endpoints);" : string.Empty
            }))),
            new($"src/{options.Name}.Api/appsettings.json", TemplateRenderer.Render(TemplateCatalog.Select("static/appsettings.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["CONNECTION_STRING"] = ConnectionString(options.Name, options.Database),
                ["WEB_PORT"] = DevelopmentWebPort(options.Name).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["JOBS_ENABLED"] = options.JobsEnabled ? "true" : "false"
            }))),
            new($"src/{options.Name}.Api/appsettings.Development.json", TemplateRenderer.Render(TemplateCatalog.Select("static/appsettings-development.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["MAIL_PROVIDER"] = options.MailProvider.ToString().ToLowerInvariant()
            }))),
            new($"src/{options.Name}.Web/package.json", TemplateRenderer.Render(TemplateCatalog.Select("static/web-package.json.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["PACKAGE_NAME"] = options.Name.ToLowerInvariant(),
                ["PACKAGE_MANAGER"] = packageManager,
                ["VERSION"] = PackageVersion()
            }))),
            new($"src/{options.Name}.Web/index.html", TemplateRenderer.Render(TemplateCatalog.Select("static/web-index.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["PROJECT_NAME"] = options.Name
            }))),
            new($"src/{options.Name}.Web/tsconfig.json", TemplateRenderer.Render(TemplateCatalog.Select("static/web-tsconfig.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/tsconfig.app.json", TemplateRenderer.Render(TemplateCatalog.Select("static/web-tsconfig.app.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/tsconfig.node.json", TemplateRenderer.Render(TemplateCatalog.Select("static/web-tsconfig.node.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/vite.config.ts", TemplateRenderer.Render(TemplateCatalog.Select("static/vite.config.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["API_PORT"] = DevelopmentApiPort(options.Name).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["WEB_PORT"] = DevelopmentWebPort(options.Name).ToString(System.Globalization.CultureInfo.InvariantCulture)
            }))),
            new($"src/{options.Name}.Web/tailwind.config.ts", TemplateRenderer.Render(TemplateCatalog.Select("static/tailwind.config.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/postcss.config.cjs", TemplateRenderer.Render(TemplateCatalog.Select("static/postcss.config.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/playwright.config.ts", TemplateRenderer.Render(TemplateCatalog.Select("static/playwright.config.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/vitest.config.ts", TemplateRenderer.Render(TemplateCatalog.Select("static/vitest.config.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/components.json", TemplateRenderer.Render(TemplateCatalog.Select("static/components.json.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/src/env.d.ts", TemplateRenderer.Render(TemplateCatalog.Select("static/web-env.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/src/main.ts", TemplateRenderer.Render(TemplateCatalog.Select("static/main.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/src/routes/index.ts", TemplateRenderer.Render(TemplateCatalog.Select(options.AuthenticationEnabled ? "static/routes-auth.template" : "static/routes-plain.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["NOTIFICATIONS_IMPORT"] = options.NotificationsEnabled ? "import NotificationsPage from '../pages/NotificationsPage.vue';\n" : string.Empty,
                ["IMPORTS_IMPORT"] = options.ImportsExportsEnabled ? "import ImportsExportsPage from '../pages/ImportsExportsPage.vue';\n" : string.Empty,
                ["WEBHOOKS_IMPORT"] = options.WebhooksEnabled ? "import WebhooksPage from '../pages/WebhooksPage.vue';\n" : string.Empty,
                ["OPTIONAL_ROUTES"] = options.AuthenticationEnabled
                    ? string.Concat(options.NotificationsEnabled ? "{ path: 'notifications', component: NotificationsPage }, " : string.Empty, options.ImportsExportsEnabled ? "{ path: 'data', component: ImportsExportsPage }, " : string.Empty, options.WebhooksEnabled ? "{ path: 'webhooks', component: WebhooksPage }, " : string.Empty)
                    : string.Concat(options.NotificationsEnabled ? ", { path: 'notifications', component: NotificationsPage }" : string.Empty, options.ImportsExportsEnabled ? ", { path: 'data', component: ImportsExportsPage }" : string.Empty, options.WebhooksEnabled ? ", { path: 'webhooks', component: WebhooksPage }" : string.Empty)
            }))),
            new($"src/{options.Name}.Web/src/App.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/web-app.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/src/style.css", TemplateRenderer.Render(TemplateCatalog.Select("static/style.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/src/dotisan/.gitkeep", string.Empty),
            new($"src/{options.Name}.Web/src/api/client.ts", TemplateRenderer.Render(TemplateCatalog.Select("static/api-client.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["ANTIFORGERY_FUNCTION"] = options.AuthenticationEnabled
                    ? "export async function issueAntiforgery(): Promise<string> {\n  const response = await fetch('/api/account/antiforgery', { credentials: 'include' });\n  if (!response.ok) throw new Error('Could not obtain an antiforgery token.');\n  return (await response.json() as { token: string }).token;\n}"
                    : string.Empty,
                ["ANTIFORGERY_HEADER"] = options.AuthenticationEnabled
                    ? "  if (method !== 'GET' && method !== 'HEAD' && method !== 'OPTIONS') headers.set('X-XSRF-TOKEN', await issueAntiforgery());"
                    : string.Empty
            }))),
            new($"src/{options.Name}.Web/src/components/ui/Button.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/ui-button.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/src/components/ui/Input.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/ui-input.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/src/components/ui/Card.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/ui-card.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/src/components/ui/Badge.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/ui-badge.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/src/lib/utils.ts", TemplateRenderer.Render(TemplateCatalog.Select("static/utils.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/src/components/ui/Badge.test.ts", TemplateRenderer.Render(TemplateCatalog.Select("static/ui-badge-test.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/src/api/client.test.ts", TemplateRenderer.Render(TemplateCatalog.Select(options.AuthenticationEnabled ? "static/api-client.test.template" : "static/api-client-plain-test.template"), TemplateContext.Empty)),
            ..(options.NotificationsEnabled || options.ImportsExportsEnabled || options.WebhooksEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Web/src/pages/integration-pages.test.ts", TemplateRenderer.Render(TemplateCatalog.Select("static/integration-pages-test.template"), new TemplateContext(new Dictionary<string, string>
                    {
                        ["NOTIFICATIONS_IMPORT"] = options.NotificationsEnabled ? "import NotificationsPage from './NotificationsPage.vue';" : string.Empty,
                        ["IMPORTS_IMPORT"] = options.ImportsExportsEnabled ? "import ImportsExportsPage from './ImportsExportsPage.vue';" : string.Empty,
                        ["WEBHOOKS_IMPORT"] = options.WebhooksEnabled ? "import WebhooksPage from './WebhooksPage.vue';" : string.Empty,
                        ["NOTIFICATIONS_TEST"] = options.NotificationsEnabled ? TemplateRenderer.Render(TemplateCatalog.Select("static/integration-notifications-test.template"), TemplateContext.Empty) : string.Empty,
                        ["IMPORTS_TEST"] = options.ImportsExportsEnabled ? TemplateRenderer.Render(TemplateCatalog.Select("static/integration-imports-test.template"), TemplateContext.Empty) : string.Empty,
                        ["WEBHOOKS_TEST"] = options.WebhooksEnabled ? TemplateRenderer.Render(TemplateCatalog.Select("static/integration-webhooks-test.template"), TemplateContext.Empty) : string.Empty
                    }))) }
                : Array.Empty<TemplateFile>()),
            new($"src/{options.Name}.Web/tests/e2e/shell.spec.ts", TemplateRenderer.Render(TemplateCatalog.Select("static/frontend-smoke.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/src/components/AppSidebar.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/app-sidebar.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["PROJECT_NAME"] = options.Name,
                ["AUTH_LINKS"] = options.AuthenticationEnabled ? "  { label: 'Profile', to: '/profile' }, { label: 'MFA security', to: '/security/mfa' }, { label: 'Sessions', to: '/security/sessions' }, { label: 'External logins', to: '/security/external-logins' }, { label: 'Authorization', to: '/admin/authorization' }," : string.Empty
            }))),
            new($"src/{options.Name}.Web/src/components/AppHeader.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/app-header.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["PROJECT_NAME"] = options.Name,
                ["AUTH_IMPORT"] = options.AuthenticationEnabled ? "import { logout } from '../dotisan/services';" : string.Empty,
                ["AUTH_FUNCTION"] = options.AuthenticationEnabled ? "async function signOut() { await logout(); window.location.assign('/auth/login'); }" : string.Empty,
                ["AUTH_MENU"] = options.AuthenticationEnabled ? "    <div class=\"user-menu\"><UiButton variant=\"ghost\" @click=\"menuOpen = !menuOpen\">Account ▾</UiButton><div v-if=\"menuOpen\" class=\"user-menu__panel\"><RouterLink to=\"/profile\">Profile</RouterLink><UiButton variant=\"ghost\" @click=\"signOut\">Sign out</UiButton></div></div>" : string.Empty
            }))),
            new($"src/{options.Name}.Web/src/layouts/PortalLayout.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/portal-layout.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/src/layouts/AuthLayout.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/auth-layout.template"), TemplateContext.Empty)),
            new($"src/{options.Name}.Web/src/pages/DashboardPage.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/dashboard-page.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["PROJECT_NAME"] = options.Name
            }))),
            ..(options.NotificationsEnabled ? new[] { new TemplateFile($"src/{options.Name}.Web/src/pages/NotificationsPage.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/notifications-page.template"), TemplateContext.Empty)) } : Array.Empty<TemplateFile>()),
            ..(options.ImportsExportsEnabled ? new[] { new TemplateFile($"src/{options.Name}.Web/src/pages/ImportsExportsPage.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/imports-exports-page.template"), TemplateContext.Empty)) } : Array.Empty<TemplateFile>()),
            ..(options.WebhooksEnabled ? new[] { new TemplateFile($"src/{options.Name}.Web/src/pages/WebhooksPage.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/webhooks-page.template"), TemplateContext.Empty)) } : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] {
                    new TemplateFile($"src/{options.Name}.Web/src/pages/auth/LoginPage.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/auth-login-page.template"), TemplateContext.Empty)),
                    new TemplateFile($"src/{options.Name}.Web/src/pages/auth/RegisterPage.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/auth-register-page.template"), TemplateContext.Empty)),
                    new TemplateFile($"src/{options.Name}.Web/src/pages/auth/ForgotPasswordPage.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/auth-forgot-password-page.template"), TemplateContext.Empty)),
                    new TemplateFile($"src/{options.Name}.Web/src/pages/auth/EmailConfirmationPage.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/auth-email-confirmation-page.template"), TemplateContext.Empty)),
                    new TemplateFile($"src/{options.Name}.Web/src/pages/auth/MfaChallengePage.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/auth-mfa-challenge-page.template"), TemplateContext.Empty)),
                    new TemplateFile($"src/{options.Name}.Web/src/pages/account/ProfilePage.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/account-profile-page.template"), TemplateContext.Empty)),
                    new TemplateFile($"src/{options.Name}.Web/src/pages/account/MfaPage.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/account-mfa-page.template"), TemplateContext.Empty)),
                    new TemplateFile($"src/{options.Name}.Web/src/pages/account/SessionsPage.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/account-sessions-page.template"), TemplateContext.Empty)),
                    new TemplateFile($"src/{options.Name}.Web/src/pages/account/ExternalLoginsPage.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/account-external-logins-page.template"), TemplateContext.Empty)),
                    new TemplateFile($"src/{options.Name}.Web/src/pages/admin/AuthorizationPage.vue", TemplateRenderer.Render(TemplateCatalog.Select("static/admin-authorization-page.template"), TemplateContext.Empty))
                }
                : Array.Empty<TemplateFile>()),
            new($"tests/{options.Name}.Api.Tests/{options.Name}.Api.Tests.csproj", TemplateRenderer.Render(TemplateCatalog.Select(options.AuthenticationEnabled ? "static/api-tests-project-auth.template" : "static/api-tests-project-plain.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["PROJECT_NAME"] = options.Name
            }))),
            new($"tests/{options.Name}.Api.Tests/HealthEndpointTests.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/health-tests.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["IDENTIFIER"] = identifier
            }))),
            ..(options.JobsEnabled ? new[] { new TemplateFile($"tests/{options.Name}.Api.Tests/JobTests.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/job-tests.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["IDENTIFIER"] = identifier,
                ["UNAUTH_STATUS"] = options.AuthenticationEnabled ? "Unauthorized" : "Accepted"
            }))) } : Array.Empty<TemplateFile>()),
            new($"tests/{options.Name}.Api.Tests/Usings.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/api-usings.template"), TemplateContext.Empty)),
            ..(options.MultiTenancyEnabled
                ? new[] { new TemplateFile($"tests/{options.Name}.Api.Tests/TenantContextTests.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/tenant-context-tests.template"), new TemplateContext(new Dictionary<string, string>
                    {
                        ["IDENTIFIER"] = identifier
                    }))) }
                : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"tests/{options.Name}.Api.Tests/AuthenticationEndpointTests.cs", TemplateRenderer.Render(TemplateCatalog.Select("static/authentication-tests.template"), new TemplateContext(new Dictionary<string, string>
                    {
                        ["IDENTIFIER"] = identifier,
                        ["REGISTRATION_TESTS"] = TemplateRenderer.Render(TemplateCatalog.Select(options.Registration == RegistrationPolicy.Public ? "static/public-authentication-tests.template" : "static/restricted-registration-test.template"), TemplateContext.Empty)
                    }))) }
                : Array.Empty<TemplateFile>()),
            ..(options.AuthenticationEnabled
                ? new[] { new TemplateFile($"src/{options.Name}.Web/src/pages/auth-pages.test.ts", TemplateRenderer.Render(TemplateCatalog.Select("static/auth-pages-test.template"), TemplateContext.Empty)) }
                : Array.Empty<TemplateFile>()),
            new($"{options.Name}.sln", TemplateRenderer.Render(TemplateCatalog.Select("static/solution.template"), new TemplateContext(new Dictionary<string, string>
            {
                ["PROJECT_NAME"] = options.Name
            })))
        ];
    }

    private static string ObservabilityServices(bool enabled) => enabled
        ? "if (args.Contains(\"--dotisan-observability\", StringComparer.OrdinalIgnoreCase)) builder.Configuration[\"OpenTelemetry:Enabled\"] = \"true\";\nvar openTelemetry = builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddEntityFrameworkCoreInstrumentation()).WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation());\nbuilder.Logging.AddOpenTelemetry(logging => logging.IncludeFormattedMessage = true);\nif (builder.Configuration.GetValue(\"OpenTelemetry:Enabled\", false)) openTelemetry.UseOtlpExporter();"
        : string.Empty;

    private static string WolverineProviderPackage(DatabaseProvider database) => database switch
    {
        DatabaseProvider.SqlServer => "SqlServer",
        DatabaseProvider.PostgreSQL => "Postgresql",
        DatabaseProvider.MySQL => "MySql",
        _ => "Sqlite"
    };

    private static string WolverinePersistenceMethod(DatabaseProvider database) => database switch
    {
        DatabaseProvider.SqlServer => "PersistMessagesWithSqlServer",
        DatabaseProvider.PostgreSQL => "PersistMessagesWithPostgresql",
        DatabaseProvider.MySQL => "PersistMessagesWithMySql",
        _ => "PersistMessagesWithSqlite"
    };

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

    private static string ConnectionString(string name, DatabaseProvider database) => database switch
    {
        DatabaseProvider.SqlServer => $"Server=localhost,1433;Database={name};User Id=sa;Password=DotisanDev123!;TrustServerCertificate=True",
        DatabaseProvider.PostgreSQL => $"Host=localhost;Database={name.ToLowerInvariant()};Username=postgres;Password=postgres",
        DatabaseProvider.MySQL => $"Server=localhost;Database={name.ToLowerInvariant()};User=root;Password=root",
        _ => $"Data Source=Data/{name}.db"
    };

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

        var volumeMarker = compose.IndexOf("\nvolumes:", StringComparison.Ordinal);
        var databaseServices = volumeMarker >= 0 ? compose[..volumeMarker] : compose;
        var volumes = volumeMarker >= 0 ? compose[volumeMarker..].TrimStart('\n') : string.Empty;
        var mailService = mailProvider == MailProvider.Mailpit
            ? "  mailpit:\n    image: axllent/mailpit:v1.21.8\n    ports:\n      - \"1025:1025\"\n      - \"8025:8025\"\n    healthcheck:\n      test: [\"CMD\", \"wget\", \"--spider\", \"-q\", \"http://localhost:8025/api/v1/info\"]\n      interval: 5s\n      timeout: 5s\n      retries: 20"
            : string.Empty;
        var dashboardService = "  dashboard:\n    image: mcr.microsoft.com/dotnet/aspire-dashboard:9.4\n    ports:\n      - \"18888:18888\"\n      - \"4317:18889\"\n      - \"4318:18890\"";

        return TemplateRenderer.Render(TemplateCatalog.Select("static/compose.template"), new TemplateContext(new Dictionary<string, string>
        {
            ["DATABASE_SERVICES"] = databaseServices.TrimEnd(),
            ["MAIL_SERVICE"] = mailService.Trim('\r', '\n'),
            ["DASHBOARD_SERVICE"] = dashboardService,
            ["VOLUMES"] = volumes
        }));
    }

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

    private static int DevelopmentApiPort(string name) => 5000 + Math.Abs(name.Aggregate(17, (hash, character) => unchecked(hash * 31 + character))) % 1000;

    private static int DevelopmentWebPort(string name) => 5173 + Math.Abs(name.Aggregate(23, (hash, character) => unchecked(hash * 31 + character))) % 1000;

    private static string PackageVersion() => typeof(TemplateFiles).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?.Split('+', 2)[0] ?? "0.0.0";

    private static string AuthenticationServices() => """
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
        if (mailProvider == "mailpit" && !builder.Environment.IsDevelopment()) throw new InvalidOperationException("Mailpit is only supported in the Development environment. Select smtp for staging or production.");
        if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing") && mailProvider == "console") throw new InvalidOperationException("Mail:Provider must be smtp or a custom provider outside Development.");
        builder.Services.AddSingleton<IEmailProvider>(services => mailProvider switch
        {
            "mailpit" => new MailpitEmailSender(services.GetRequiredService<IHttpClientFactory>(), services.GetRequiredService<IConfiguration>()),
            "smtp" => new SmtpEmailSender(services.GetRequiredService<IConfiguration>()),
            _ => new ConsoleEmailSender()
        });
        builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddCookie(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
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
        builder.Services.AddAuthorization(options => { foreach (var permission in Permissions.All) options.AddPolicy(permission, policy => policy.RequireClaim(Permissions.ClaimType, permission)); });
        builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");
        builder.Services.AddRateLimiter(options => options.AddPolicy("account", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
        """;

}
