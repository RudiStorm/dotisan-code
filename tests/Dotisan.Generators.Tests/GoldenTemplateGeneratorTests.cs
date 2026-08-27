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
            Assert.Contains("\"version\": \"0.2.0\"", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Web", "package.json")));
            Assert.Contains("UseSqlite", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "Program.cs")));
            Assert.Contains("Data Source=app.db", await File.ReadAllTextAsync(Path.Combine(output, "src", "TodoApp.Api", "appsettings.json")));
            Assert.Contains("profile: quick", await File.ReadAllTextAsync(Path.Combine(output, "dotisan.config")));
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
            Assert.Contains("public sealed class ApplicationUser : IdentityUser", user);
            Assert.DoesNotContain("src/PlainApp.Api/Identity/ApplicationUser.cs", plainPaths);
            Assert.Contains("src/AuthApp.Api/Features/Account/AccountEndpoints.cs", authenticatedPaths);
            Assert.Contains("src/AuthApp.Api/Features/Authorization/AuthorizationEndpoints.cs", authenticatedPaths);
            Assert.DoesNotContain("src/PlainApp.Api/Features/Authorization/AuthorizationEndpoints.cs", plainPaths);
            var authorizationEndpoints = await File.ReadAllTextAsync(Path.Combine(authenticatedOutput, "src", "AuthApp.Api", "Features", "Authorization", "AuthorizationEndpoints.cs"));
            Assert.Contains("MapGet(\"/api/authorization/profile\"", authorizationEndpoints);
            Assert.Contains("RequireAuthorization(Permissions.ProfileView)", authorizationEndpoints);
            Assert.Contains("src/AuthApp.Api/Authorization/Permissions.cs", authenticatedPaths);
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
            Assert.DoesNotContain("AccountEndpoints.cs", plainPaths);
            Assert.Contains("AddIdentityCore<ApplicationUser>", authenticatedProgram);
            Assert.Contains("AddRoles<IdentityRole>()", authenticatedProgram);
            Assert.Contains("AddEntityFrameworkStores<AppDbContext>", authenticatedProgram);
            Assert.Contains("AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)", authenticatedProgram);
            Assert.Contains("AddAuthorization(options =>", authenticatedProgram);
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
            Assert.Contains("--auth yes", authenticatedReadme);
            Assert.Contains("InitialIdentity", authenticatedReadme);
            Assert.Contains("dotisan migrate", authenticatedReadme);
            Assert.Contains("does not create migrations", authenticatedReadme);
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
