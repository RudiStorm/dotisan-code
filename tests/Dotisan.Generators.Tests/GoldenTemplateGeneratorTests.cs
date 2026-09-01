using Dotisan.Core;
using Dotisan.Generators;

namespace Dotisan.Generators.Tests;

public sealed class GoldenTemplateGeneratorTests
{
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
            Assert.True(File.Exists(Path.Combine(output, "src", "TodoApp.Web", "package.json")));
            var generatedModels = Path.Combine(output, "src", "TodoApp.Web", "src", "generated", "models.ts");
            Assert.True(File.Exists(generatedModels));
            Assert.Equal(Environment.NewLine, await File.ReadAllTextAsync(generatedModels));
            Assert.Contains("\"version\": \"0.4.0\"", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Web", "package.json")));
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
            Assert.Contains("Data Source=app.db", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "appsettings.json")));
            Assert.Contains("profile: quick", await File.ReadAllTextAsync(Path.Combine(output, "dotisan.config")));
            var readme = await File.ReadAllTextAsync(Path.Combine(output, "README.md"));
            Assert.Contains("AuditEntry", readme);
            Assert.Contains("Audit__Enabled", readme);
            Assert.Contains("InitialAudit", readme);
            Assert.Contains("checks the .NET SDK", readme);
            Assert.Contains("Press Ctrl+C once", readme);
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(DatabaseProvider.SQLite, "Microsoft.EntityFrameworkCore.Sqlite", "UseSqlite", "Data Source=app.db", "SQLite")]
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
            if (provider != DatabaseProvider.SQLite)
            {
                Assert.DoesNotContain("Microsoft.EntityFrameworkCore.Sqlite", apiProject);
                var compose = await File.ReadAllTextAsync(Path.Combine(output, "compose.yaml"));
                Assert.Contains("services:", compose);
                Assert.Contains("database:", compose);
                Assert.Contains(provider switch
                {
                    DatabaseProvider.SqlServer => "mcr.microsoft.com/mssql/server:2022-latest",
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
                Assert.False(File.Exists(Path.Combine(output, "compose.yaml")));
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
            Assert.Contains("[ProfileView, CustomersView, CustomersCreate, CustomersUpdate, CustomersDelete, OrdersView, OrdersCreate, OrdersUpdate, OrdersDelete]", permissions);
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
            var plainProgram = await File.ReadAllTextAsync(Path.Combine(plainOutput, "src", "PlainApp.Api", "Program.cs"));
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
            Assert.Contains("AuthenticationScheme = CookieAuthenticationDefaults.AuthenticationScheme", accountEndpoints);
            Assert.Contains("IAuditWriter", accountEndpoints);
            Assert.Contains("security.registered", accountEndpoints);
            Assert.Contains("security.registration.denied", accountEndpoints);
            Assert.Contains("security.login.succeeded", accountEndpoints);
            Assert.Contains("security.login.failed", accountEndpoints);
            Assert.Contains("security.logout", accountEndpoints);
            Assert.DoesNotContain("AccountEndpoints.cs", plainPaths);
            Assert.Contains("AddIdentityCore<ApplicationUser>", authenticatedProgram);
            Assert.Contains("AddRoles<IdentityRole>()", authenticatedProgram);
            Assert.Contains("AddEntityFrameworkStores<AppDbContext>", authenticatedProgram);
            Assert.Contains("AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)", authenticatedProgram);
            Assert.Contains("AddAuthorization(options =>", authenticatedProgram);
            Assert.Contains("AddScoped<IAuditWriter, AuditWriter>", authenticatedProgram);
            Assert.Contains("Permissions.All", authenticatedProgram);
            Assert.Contains("RequireClaim(Permissions.ClaimType, permission)", authenticatedProgram);
            Assert.Contains("UseAuthentication", authenticatedProgram);
            Assert.Contains("UseAuthorization", authenticatedProgram);
            Assert.Contains("UseAntiforgery", authenticatedProgram);
            Assert.Contains("MapAccountEndpoints", authenticatedProgram);
            Assert.Contains("MapAuthorizationEndpoints", authenticatedProgram);
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
            Assert.Contains("does not create migrations", authenticatedReadme);
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
}
