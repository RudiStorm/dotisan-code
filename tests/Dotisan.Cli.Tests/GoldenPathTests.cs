using Dotisan.Cli;
using Dotisan.Cli.Generation;
using Dotisan.Core;
using Dotisan.OpenApi;
using Dotisan.Testing;

namespace Dotisan.Cli.Tests;

public sealed class GoldenPathTests
{
    [Fact]
    public async Task Resource_generation_updates_contract_and_preserves_a_buildable_frontend()
    {
        using var directory = new TemporaryDirectory();
        var root = Path.Combine(directory.Path, "GoldenApp");

        var creationConsole = new MemoryConsole();
        var creation = DotisanApplication.CreateDefault(creationConsole);
        Assert.Equal(
            DotisanExitCode.Success,
            await creation.RunAsync(["new", "GoldenApp", "--yes", "--auth", "yes", "--registration", "public", "--no-restore", "--output", root]));

        var console = new MemoryConsole();
        var app = DotisanApplication.CreateDefault(console, new DefaultDotisanServices(root));
        Assert.Equal(DotisanExitCode.Success, await app.RunAsync(["make:resource", "Customer"]));
        var customerManifest = new ContractManifest(
            1,
            [
                new EndpointManifestEntry("listCustomers", "Customers", "listCustomers", "GET", "/api/customers", "void", "global::Customer", false, null, null, ["Customers"], false, false),
                new EndpointManifestEntry("getCustomer", "Customers", "getCustomer", "GET", "/api/customers/{id:guid}", "void", "global::Customer", false, null, null, ["Customers"], false, false)
            ],
            [new ContractModel("Customer", "global::Customer", [new ContractProperty("customerId", new ContractTypeDescriptor(ContractTypeKind.Guid), false, false)], [])],
            [
                EndpointContractMetadata.Create("GET", "/api/customers", null, [], ["Customers"], false),
                EndpointContractMetadata.Create("GET", "/api/customers/{id:guid}", null, [new EndpointParameterMetadata("id", new ContractTypeDescriptor(ContractTypeKind.Guid), false, false)], ["Customers"], false)
            ]);
        var apiObj = Path.Combine(root, "src", "GoldenApp.Api", "obj");
        Directory.CreateDirectory(apiObj);
        await File.WriteAllTextAsync(Path.Combine(apiObj, "GoldenApp.Api.json"), OpenApiDocumentGenerator.Generate(customerManifest, "GoldenApp", "v1").Json);
        var generation = await ContractGenerationService.GenerateAsync(root, check: false, CancellationToken.None, noOpenApi: true, services: new DefaultDotisanServices(root), console: console);
        Assert.True(generation.Success, generation.ErrorMessage);

        var generatedServices = await File.ReadAllTextAsync(
            Path.Combine(root, "src", "GoldenApp.Web", "src", "dotisan", "features", "customers", "services.ts"));
        Assert.Contains("listCustomers", generatedServices, StringComparison.Ordinal);
        var generatedModels = await File.ReadAllTextAsync(
            Path.Combine(root, "src", "GoldenApp.Web", "src", "dotisan", "features", "customers", "models.ts"));
        Assert.Contains("customerId", generatedModels, StringComparison.Ordinal);
    }

    private sealed class MemoryConsole : IConsole
    {
        public string ErrorOutput { get; private set; } = string.Empty;

        public void WriteLine(string message) { }
        public void WriteError(string message) => ErrorOutput += message + Environment.NewLine;
    }
}
