using Dotisan.Core;

namespace Dotisan.Core.Tests;

public sealed class EndpointManifestTests
{
    [Fact]
    public void Manifest_json_is_deterministic_and_contains_contract_fields()
    {
        var manifest = new EndpointManifest([
            new EndpointManifestEntry(
                Id: "customers.create",
                Feature: "Customers",
                Name: "CreateCustomer",
                Method: "POST",
                Route: "/api/customers",
                Request: "CreateCustomer.Request",
                Response: "CreateCustomer.Response",
                Authorization: true,
                Permission: "customers.create",
                Version: null,
                Tags: ["Customers"],
                Validation: true,
                Deprecated: false)
        ]);

        var json = manifest.ToJson();

        Assert.Contains("\"id\":\"customers.create\"", json);
        Assert.Contains("\"method\":\"POST\"", json);
        Assert.DoesNotContain("\n", json);
        Assert.Equal(json, manifest.ToJson());
    }
}
