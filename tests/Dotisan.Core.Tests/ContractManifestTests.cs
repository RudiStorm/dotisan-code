using System.Security.Cryptography;
using System.Text;

namespace Dotisan.Core.Tests;

public sealed class ContractManifestTests
{
    [Fact]
    public void Orders_models_properties_and_enum_values_and_serializes_camel_case()
    {
        var manifest = new ContractManifest(
            1,
            [],
            [
                new ContractModel("ZModel", "global::Z", [
                    new ContractProperty("zValue", new ContractTypeDescriptor(ContractTypeKind.Integer), false, false),
                    new ContractProperty("aValue", new ContractTypeDescriptor(ContractTypeKind.String), true, true)
                ], []),
                new ContractModel("AEnum", "global::AEnum", [], [
                    new ContractEnumValue("Second", 2),
                    new ContractEnumValue("First", 1)
                ])
            ]);

        Assert.Equal(["AEnum", "ZModel"], manifest.Models.Select(model => model.Name));
        Assert.Equal(["aValue", "zValue"], manifest.Models[1].Properties.Select(property => property.Name));
        Assert.Equal(["First", "Second"], manifest.Models[0].EnumValues.Select(value => value.Name));
        Assert.Contains("\"schemaVersion\":1", manifest.ToJson());
        Assert.Contains("\"enumValues\":[{\"name\":\"First\",\"value\":1}", manifest.ToJson());
        Assert.Contains("\"type\":{\"kind\":\"string\"", manifest.ToJson());
        Assert.Contains("\"elementType\"", manifest.ToJson());
        Assert.Contains("\"nullable\":true", manifest.ToJson());
        Assert.Contains("\"optional\":true", manifest.ToJson());
    }

    [Fact]
    public void Hash_is_stable_for_equivalent_input_order()
    {
        var first = CreateManifest(modelsInReverseOrder: false);
        var second = CreateManifest(modelsInReverseOrder: true);

        Assert.Equal(first.ToJson(), second.ToJson());
        Assert.Equal(first.Sha256, second.Sha256);
    }

    [Fact]
    public void Rejects_non_v1_schema_version()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContractManifest(
            2,
            [],
            []));
    }

    [Fact]
    public void Rejects_invalid_descriptor_references()
    {
        Assert.Throws<ArgumentException>(() => new ContractTypeDescriptor(ContractTypeKind.Object));
        Assert.Throws<ArgumentException>(() => new ContractTypeDescriptor(
            ContractTypeKind.Array,
            ElementType: null));
    }

    [Fact]
    public void Supports_dictionary_value_descriptors_and_valid_references()
    {
        var manifest = new ContractManifest(
            1,
            [],
            [
                new ContractModel("DictionaryModel", "global::DictionaryModel", [
                    new ContractProperty(
                        "items",
                        new ContractTypeDescriptor(
                            ContractTypeKind.Dictionary,
                            ElementType: new ContractTypeDescriptor(ContractTypeKind.Enum, "global::ItemKind")),
                        false,
                        false),
                    new ContractProperty(
                        "owner",
                        new ContractTypeDescriptor(ContractTypeKind.Object, "global::Owner"),
                        true,
                        true)
                ], [])
            ]);

        var json = manifest.ToJson();

        Assert.Contains("\"kind\":\"dictionary\"", json);
        Assert.Contains("\"elementType\":{\"kind\":\"enum\",\"referenceName\":\"global::ItemKind\"", json);
        Assert.Contains("\"referenceName\":\"global::Owner\"", json);
        Assert.Contains("\"nullable\":true", json);
        Assert.Contains("\"optional\":true", json);
    }

    [Fact]
    public void Copies_and_sorts_model_collections_at_construction_time()
    {
        var properties = new List<ContractProperty>
        {
            new("zValue", new ContractTypeDescriptor(ContractTypeKind.Integer), false, false),
            new("aValue", new ContractTypeDescriptor(ContractTypeKind.String), false, false)
        };

        var enumValues = new List<ContractEnumValue>
        {
            new("Second", 2),
            new("First", 1)
        };

        var model = new ContractModel("Model", "global::Model", properties, enumValues);

        properties.Add(new ContractProperty("mutated", new ContractTypeDescriptor(ContractTypeKind.Boolean), false, false));
        enumValues.Add(new ContractEnumValue("Mutated", 3));

        Assert.Equal(["aValue", "zValue"], model.Properties.Select(property => property.Name));
        Assert.Equal(["First", "Second"], model.EnumValues.Select(value => value.Name));
        Assert.DoesNotContain(model.Properties, property => property.Name == "mutated");
        Assert.DoesNotContain(model.EnumValues, value => value.Name == "Mutated");
    }

    [Fact]
    public void Sorts_endpoints_using_existing_manifest_ordering()
    {
        var manifest = new ContractManifest(
            1,
            [
                Entry("z.read"),
                Entry("a.read")
            ],
            []);

        Assert.Equal(["a.read", "z.read"], manifest.Endpoints.Select(endpoint => endpoint.Id));
    }

    private static ContractManifest CreateManifest(bool modelsInReverseOrder)
    {
        var models = modelsInReverseOrder
            ? new[]
            {
                new ContractModel("ZModel", "global::Z", [], []),
                new ContractModel("AModel", "global::A", [], [])
            }
            : new[]
            {
                new ContractModel("AModel", "global::A", [], []),
                new ContractModel("ZModel", "global::Z", [], [])
            };

        return new ContractManifest(1, [Entry("b.read"), Entry("a.read")], models);
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
