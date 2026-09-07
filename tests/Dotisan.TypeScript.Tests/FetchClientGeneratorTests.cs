using Dotisan.Core;
using Dotisan.TypeScript;

namespace Dotisan.TypeScript.Tests;

public sealed class FetchClientGeneratorTests
{
    [Fact]
    public void Generates_encoded_path_and_optional_query_parameters()
    {
        var manifest = new ContractManifest(
            1,
            [new EndpointManifestEntry("users.get", "Users", "getUser", "GET", "/api/users/{id:guid}", "void", "global::User", false, null, null, [], false, false)],
            [new ContractModel("User", "global::User", [], [])],
            [EndpointContractMetadata.Create(
                "GET",
                "/api/users/{id:guid}",
                null,
                [new EndpointParameterMetadata("id", new ContractTypeDescriptor(ContractTypeKind.Guid), false, false), new EndpointParameterMetadata("includeHistory", new ContractTypeDescriptor(ContractTypeKind.Boolean), false, true)],
                [],
                false)]);

        var output = FetchClientGenerator.Generate(manifest).Content;

        Assert.Contains("id: string", output, StringComparison.Ordinal);
        Assert.Contains("query: { includeHistory?: boolean } = {}", output, StringComparison.Ordinal);
        Assert.Contains("encodeURIComponent(String(id))", output, StringComparison.Ordinal);
        Assert.Contains("search.set(\"includeHistory\", String(query.includeHistory))", output, StringComparison.Ordinal);
    }
}
