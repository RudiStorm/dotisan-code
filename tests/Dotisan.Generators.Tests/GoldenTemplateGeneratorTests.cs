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
}
