using Dotisan.Core;
using System.Security.Cryptography;
using System.Text;

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

    [Fact]
    public void Manifest_sorts_entries_and_exposes_a_stable_sha256_hash()
    {
        var manifest = new EndpointManifest([
            Entry("z.read"),
            Entry("a.read")
        ]);

        var json = manifest.ToJson();
        var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();

        Assert.Equal(1, EndpointManifest.SchemaVersion);
        Assert.True(json.IndexOf("\"id\":\"a.read\"", StringComparison.Ordinal) <
                    json.IndexOf("\"id\":\"z.read\"", StringComparison.Ordinal));
        Assert.Equal(expectedHash, manifest.Sha256);
        Assert.Equal(manifest.Sha256, manifest.Sha256);
    }

    private static EndpointManifestEntry Entry(string id) => new(
        Id: id,
        Feature: "Test",
        Name: id,
        Method: "GET",
        Route: $"/api/{id}",
        Request: "Request",
        Response: "Response",
        Authorization: false,
        Permission: null,
        Version: null,
        Tags: [],
        Validation: false,
        Deprecated: false);
}
