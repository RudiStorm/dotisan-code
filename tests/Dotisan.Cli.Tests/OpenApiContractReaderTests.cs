using Dotisan.Cli.Generation;
using Dotisan.Core;
using Dotisan.OpenApi;

namespace Dotisan.Cli.Tests;

public sealed class OpenApiContractReaderTests
{
    [Fact]
    public void Reads_routes_parameters_models_and_responses_from_openapi()
    {
        var source = new ContractManifest(
            1,
            [new EndpointManifestEntry("users.get", "Users", "GetUser", "GET", "/api/users/{id}", "void", "global::User", false, null, null, ["Users"], false, false)],
            [new ContractModel("User", "global::User", [new ContractProperty("displayName", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], [])],
            [new EndpointContractMetadata(null, [new EndpointParameterMetadata("id", new ContractTypeDescriptor(ContractTypeKind.Guid), false, false)], [], 200, ["Users"], new EndpointValidationMetadata(false, []))]);

        var openApi = OpenApiDocumentGenerator.Generate(source, "Test", "v1").Json;
        var restored = OpenApiContractReader.Read(openApi);

        Assert.Equal("users.get", restored.Endpoints.Single().Id);
        Assert.Equal("/api/users/{id}", restored.Endpoints.Single().Route);
        Assert.Equal(ContractTypeKind.Guid, restored.EndpointMetadata!.Single().PathParameters.Single().Type.Kind);
        Assert.Equal("User", restored.Endpoints.Single().Response);
        Assert.Equal("displayName", restored.Models.Single().Properties.Single().Name);
    }
}
