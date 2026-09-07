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
    public void Preserves_legacy_json_when_endpoint_metadata_is_not_provided()
    {
        var manifest = new ContractManifest(
            1,
            [Entry("customers.read")],
            []);

        Assert.DoesNotContain("\"endpointMetadata\"", manifest.ToJson());
        Assert.Null(manifest.EndpointMetadata);
    }

    [Fact]
    public void Preserves_original_three_parameter_constructor_signature()
    {
        var constructor = typeof(ContractManifest).GetConstructor(
            [
                typeof(int),
                typeof(IReadOnlyList<EndpointManifestEntry>),
                typeof(IReadOnlyList<ContractModel>)
            ]);

        Assert.NotNull(constructor);
    }

    [Fact]
    public void Sorts_endpoint_transport_metadata_with_endpoints_and_serializes_transport_details()
    {
        var manifest = new ContractManifest(
            1,
            [
                Entry("customers.write"),
                Entry("customers.read")
            ],
            [],
            [
                EndpointContractMetadata.Create(
                    "POST",
                    "/api/customers/{id:guid}",
                    new EndpointRequestBodyMetadata(
                        new ContractTypeDescriptor(ContractTypeKind.Object, "CreateCustomerRequest")),
                    [
                        new EndpointParameterMetadata("id", new ContractTypeDescriptor(ContractTypeKind.Guid), false, false),
                        new EndpointParameterMetadata("display_name", new ContractTypeDescriptor(ContractTypeKind.String), true, true)
                    ],
                    ["Writes", "Customers"],
                    validation: true),
                EndpointContractMetadata.Create(
                    "GET",
                    "/api/customers/{id:int}",
                    new EndpointRequestBodyMetadata(
                        new ContractTypeDescriptor(ContractTypeKind.Object, "ReadCustomerRequest")),
                    [
                        new EndpointParameterMetadata("id", new ContractTypeDescriptor(ContractTypeKind.Integer), false, false),
                        new EndpointParameterMetadata("display_name", new ContractTypeDescriptor(ContractTypeKind.String), true, true)
                    ],
                    ["Reads"],
                    validation: false)
            ]);

        Assert.Equal(["customers.read", "customers.write"], manifest.Endpoints.Select(endpoint => endpoint.Id));
        Assert.NotNull(manifest.EndpointMetadata);
        Assert.Equal(2, manifest.EndpointMetadata!.Count);

        var read = manifest.EndpointMetadata[0];
        Assert.Null(read.RequestBody);
        Assert.Equal(200, read.SuccessStatusCode);
        Assert.Equal(["id"], read.PathParameters.Select(parameter => parameter.Name));
        Assert.Equal(ContractTypeKind.Integer, read.PathParameters[0].Type.Kind);
        Assert.Equal(["display_name"], read.QueryParameters.Select(parameter => parameter.Name));
        Assert.Equal(["Reads"], read.Tags);
        Assert.False(read.Validation.Enabled);

        var write = manifest.EndpointMetadata[1];
        Assert.NotNull(write.RequestBody);
        Assert.Equal("CreateCustomerRequest", write.RequestBody!.Type.ReferenceName);
        Assert.Equal(201, write.SuccessStatusCode);
        Assert.Equal(["Customers", "Writes"], write.Tags);
        Assert.True(write.Validation.Enabled);
        Assert.Empty(write.QueryParameters);

        var json = manifest.ToJson();
        Assert.Contains("\"endpointMetadata\":[{\"requestBody\":null", json);
        Assert.Contains("\"successStatusCode\":200", json);
        Assert.Contains("\"successStatusCode\":201", json);
        Assert.Contains("\"referenceName\":\"CreateCustomerRequest\"", json);
        Assert.Contains("\"queryParameters\":[{\"name\":\"display_name\"", json);
    }

    [Fact]
    public void Json_round_trip_preserves_endpoint_metadata_and_hash()
    {
        var original = new ContractManifest(
            1,
            [Entry("customers.read")],
            [],
            [EndpointContractMetadata.Create(
                "GET",
                "/api/customers/{id:guid}",
                requestBody: null,
                [new EndpointParameterMetadata("id", new ContractTypeDescriptor(ContractTypeKind.Guid), false, false)],
                ["Customers"],
                validation: true)]);

        var restored = ContractManifest.FromJson(original.ToJson());

        Assert.NotNull(restored.EndpointMetadata);
        Assert.Equal(original.ToJson(), restored.ToJson());
        Assert.Equal(original.Sha256, restored.Sha256);
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

    [Fact]
    public void Infers_route_token_path_parameters_without_request_properties()
    {
        var metadata = EndpointContractMetadata.Create(
            "GET",
            "/api/customers/{id:guid}/{version:int?}",
            requestBody: null,
            [
                new EndpointParameterMetadata("search", new ContractTypeDescriptor(ContractTypeKind.String), true, true)
            ],
            [],
            validation: false);

        Assert.Equal(["id", "version"], metadata.PathParameters.Select(parameter => parameter.Name));
        Assert.Equal(ContractTypeKind.Guid, metadata.PathParameters[0].Type.Kind);
        Assert.Equal(ContractTypeKind.Integer, metadata.PathParameters[1].Type.Kind);
        Assert.True(metadata.PathParameters[1].Optional);
        Assert.Equal(["search"], metadata.QueryParameters.Select(parameter => parameter.Name));
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
