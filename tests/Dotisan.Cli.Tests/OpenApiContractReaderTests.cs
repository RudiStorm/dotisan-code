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

    [Fact]
    public void Preserves_operation_security_requirement()
    {
        const string json = """
        {"openapi":"3.0.1","info":{"title":"Test","version":"v1"},"security":[{"cookieAuth":[]}],"paths":{"/api/private":{"get":{"operationId":"private.get","responses":{"200":{"description":"ok"}}}}}}
        """;

        var restored = OpenApiContractReader.Read(json);

        Assert.True(restored.Endpoints.Single().Authorization);
    }

    [Fact]
    public void Treats_non_json_or_empty_content_as_void_instead_of_throwing()
    {
        const string json = """
        {"openapi":"3.0.1","info":{"title":"Test","version":"v1"},"paths":{"/api/customers":{"post":{"operationId":"customers.create","requestBody":{"content":{"text/plain":{"schema":{"type":"string"}}}},"responses":{"204":{"description":"no content"}}}}}}
        """;

        var restored = OpenApiContractReader.Read(json);

        Assert.Equal("void", restored.Endpoints.Single().Request);
        Assert.Equal("void", restored.Endpoints.Single().Response);
    }

    [Fact]
    public void Defaults_parameter_required_to_false_when_openapi_omits_it()
    {
        const string json = """
        {"openapi":"3.0.1","info":{"title":"Test","version":"v1"},"paths":{"/api/customers":{"get":{"operationId":"customers.list","parameters":[{"name":"filter","in":"query","schema":{"type":"string"}}],"responses":{"200":{"description":"ok"}}}}}}
        """;

        var restored = OpenApiContractReader.Read(json);

        Assert.True(restored.EndpointMetadata!.Single().QueryParameters.Single().Optional);
    }
}
