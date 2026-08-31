using Dotisan.Core;
using Dotisan.OpenApi;
using Xunit;

namespace Dotisan.OpenApi.Tests;

public sealed class OpenApiDocumentGeneratorTests
{
    [Fact]
    public void Generates_deterministic_openapi_json_for_manifest_transport_metadata()
    {
        var first = CreateManifest(reverseInputs: false);
        var second = CreateManifest(reverseInputs: true);

        var generated = OpenApiDocumentGenerator.Generate(first, "Dotisan Sample API", "v1");
        var regenerated = OpenApiDocumentGenerator.Generate(second, "Dotisan Sample API", "v1");

        var expectedJson = """
            {"openapi":"3.1.0","jsonSchemaDialect":"https://spec.openapis.org/oas/3.1/dialect/base","info":{"title":"Dotisan Sample API","version":"v1"},"paths":{"/orders":{"post":{"operationId":"orders.create","tags":["Orders","Writes"],"requestBody":{"required":true,"content":{"application/json":{"schema":{"$ref":"#/components/schemas/CreateOrderRequest"}}}},"responses":{"201":{"description":"Success","content":{"application/json":{"schema":{"$ref":"#/components/schemas/OrderDetail"}}}}}}},"/orders/{orderId}":{"get":{"operationId":"orders.get","tags":["Orders","Reads"],"parameters":[{"name":"orderId","in":"path","required":true,"schema":{"type":"integer"}},{"name":"includeHistory","in":"query","required":false,"schema":{"type":["boolean","null"]}}],"responses":{"200":{"description":"Success","content":{"application/json":{"schema":{"$ref":"#/components/schemas/OrderDetail"}}}}}}},"/orders/{orderId}/items/{itemId}":{"put":{"operationId":"orders.item.update","tags":["Items","Orders"],"parameters":[{"name":"orderId","in":"path","required":true,"schema":{"type":"integer"}},{"name":"itemId","in":"path","required":true,"schema":{"type":"string","format":"uuid"}}],"requestBody":{"required":true,"content":{"application/json":{"schema":{"$ref":"#/components/schemas/UpdateOrderItemRequest"}}}},"responses":{"200":{"description":"Success","content":{"application/json":{"schema":{"$ref":"#/components/schemas/OrderItem"}}}}}}}},"components":{"schemas":{"CreateOrderItem":{"type":"object","properties":{"productCode":{"type":"string"},"quantity":{"type":"integer"}},"required":["productCode","quantity"]},"CreateOrderRequest":{"type":"object","properties":{"attributes":{"type":"object","additionalProperties":{"type":"string"}},"customerId":{"type":"string","format":"uuid"},"items":{"type":"array","items":{"$ref":"#/components/schemas/CreateOrderItem"}},"notes":{"type":["string","null"]},"priority":{"$ref":"#/components/schemas/OrderPriority"}},"required":["attributes","customerId","items","priority"]},"OrderDetail":{"type":"object","properties":{"attributes":{"type":"object","additionalProperties":{"type":"string"}},"id":{"type":"integer"},"items":{"type":"array","items":{"$ref":"#/components/schemas/OrderItem"}},"metadata":{"type":"object","additionalProperties":{"$ref":"#/components/schemas/OrderMetadataEntry"}},"priority":{"$ref":"#/components/schemas/OrderPriority"},"submittedAt":{"type":"string","format":"date-time"}},"required":["attributes","id","items","metadata","priority","submittedAt"]},"OrderItem":{"type":"object","properties":{"itemId":{"type":"string","format":"uuid"},"productCode":{"type":"string"},"quantity":{"type":"integer"}},"required":["itemId","productCode","quantity"]},"OrderMetadataEntry":{"type":"object","properties":{"capturedOn":{"type":"string","format":"date"},"shiftStart":{"type":"string","format":"time"},"source":{"type":"string"}},"required":["capturedOn","shiftStart","source"]},"OrderPriority":{"type":"integer","enum":[0,10],"x-enumNames":["Normal","Rush"]},"UpdateOrderItemRequest":{"type":"object","properties":{"metadata":{"type":"object","additionalProperties":{"$ref":"#/components/schemas/OrderMetadataEntry"}},"priority":{"$ref":"#/components/schemas/OrderPriority"},"quantity":{"type":["integer","null"]}},"required":["priority","quantity"]}}}}
            """;

        Assert.Equal(expectedJson, generated.Json);
        Assert.Equal(generated.Json, regenerated.Json);
        Assert.Equal("1202e8097a3533cc95ec613d73c041fbf5b4501e5155a0929ae363c650383b72", generated.Sha256);
        Assert.Equal(generated.Sha256, regenerated.Sha256);
    }

    [Fact]
    public void Rejects_unsupported_types_with_actionable_error()
    {
        var manifest = new ContractManifest(
            1,
            [
                new EndpointManifestEntry(
                    Id: "support.echo",
                    Feature: "Support",
                    Name: "support.echo",
                    Method: "GET",
                    Route: "/support/{ticketId}",
                    Request: "global::SupportRequest",
                    Response: "global::SupportResponse",
                    Authorization: false,
                    Permission: null,
                    Version: null,
                    Tags: [],
                    Validation: false,
                    Deprecated: false)
            ],
            [
                new ContractModel(
                    "SupportResponse",
                    "global::SupportResponse",
                    [
                        new ContractProperty("ticketId", new ContractTypeDescriptor(ContractTypeKind.Unknown), false, false)
                    ],
                    [])
            ],
            [
                EndpointContractMetadata.Create(
                    "GET",
                    "/support/{ticketId}",
                    requestBody: null,
                    [
                        new EndpointParameterMetadata("ticketId", new ContractTypeDescriptor(ContractTypeKind.String), false, false)
                    ],
                    ["Support"],
                    validation: false)
            ]);

        var error = Assert.Throws<NotSupportedException>(() => OpenApiDocumentGenerator.Generate(manifest, "Support API", "v1"));
        Assert.Equal("OpenAPI generation does not support contract type kind 'Unknown' for 'SupportResponse.ticketId'.", error.Message);
    }

    [Fact]
    public void Rejects_optional_route_parameters_with_actionable_error()
    {
        var manifest = new ContractManifest(
            1,
            [
                new EndpointManifestEntry(
                    Id: "orders.optional",
                    Feature: "Orders",
                    Name: "orders.optional",
                    Method: "GET",
                    Route: "/orders/{orderId?}",
                    Request: "global::OptionalOrderRequest",
                    Response: "global::OptionalOrderResponse",
                    Authorization: false,
                    Permission: null,
                    Version: null,
                    Tags: [],
                    Validation: false,
                    Deprecated: false)
            ],
            [
                new ContractModel(
                    "OptionalOrderResponse",
                    "global::OptionalOrderResponse",
                    [
                        new ContractProperty("id", new ContractTypeDescriptor(ContractTypeKind.Integer), false, false)
                    ],
                    [])
            ],
            [
                EndpointContractMetadata.Create(
                    "GET",
                    "/orders/{orderId?}",
                    requestBody: null,
                    [],
                    ["Orders"],
                    validation: false)
            ]);

        var error = Assert.Throws<InvalidOperationException>(() => OpenApiDocumentGenerator.Generate(manifest, "Orders API", "v1"));
        Assert.Equal(
            "OpenAPI path parameters must be required; endpoint 'orders.optional' declares optional route parameter 'orderId'.",
            error.Message);
    }

    [Fact]
    public void Omits_response_content_for_204_no_content_operations()
    {
        var manifest = new ContractManifest(
            1,
            [
                new EndpointManifestEntry(
                    Id: "orders.delete",
                    Feature: "Orders",
                    Name: "orders.delete",
                    Method: "DELETE",
                    Route: "/orders/{orderId}",
                    Request: "global::DeleteOrderRequest",
                    Response: "global::DeleteOrderResponse",
                    Authorization: false,
                    Permission: null,
                    Version: null,
                    Tags: [],
                    Validation: false,
                    Deprecated: false)
            ],
            [],
            [
                EndpointContractMetadata.Create(
                    "DELETE",
                    "/orders/{orderId}",
                    requestBody: null,
                    [
                        new EndpointParameterMetadata("orderId", new ContractTypeDescriptor(ContractTypeKind.Integer), false, false)
                    ],
                    ["Orders", "Deletes"],
                    validation: false)
            ]);

        var generated = OpenApiDocumentGenerator.Generate(manifest, "Orders API", "v1");
        var expectedJson = """
            {"openapi":"3.1.0","jsonSchemaDialect":"https://spec.openapis.org/oas/3.1/dialect/base","info":{"title":"Orders API","version":"v1"},"paths":{"/orders/{orderId}":{"delete":{"operationId":"orders.delete","tags":["Deletes","Orders"],"parameters":[{"name":"orderId","in":"path","required":true,"schema":{"type":"integer"}}],"responses":{"204":{"description":"Success"}}}}},"components":{"schemas":{}}}
            """;

        Assert.Equal(expectedJson, generated.Json);
        Assert.DoesNotContain("\"content\"", generated.Json, StringComparison.Ordinal);
    }

    private static ContractManifest CreateManifest(bool reverseInputs)
    {
        var endpoints = reverseInputs
            ? new[]
            {
                Entry("orders.item.update", "PUT", "/orders/{orderId}/items/{itemId}"),
                Entry("orders.get", "GET", "/orders/{orderId}"),
                Entry("orders.create", "POST", "/orders")
            }
            : new[]
            {
                Entry("orders.create", "POST", "/orders"),
                Entry("orders.get", "GET", "/orders/{orderId}"),
                Entry("orders.item.update", "PUT", "/orders/{orderId}/items/{itemId}")
            };

        var models = reverseInputs
            ? new[]
            {
                OrderPriorityModel(),
                UpdateOrderItemRequestModel(),
                OrderMetadataEntryModel(),
                OrderItemModel(),
                OrderDetailModel(),
                CreateOrderItemModel(),
                CreateOrderRequestModel()
            }
            : new[]
            {
                CreateOrderRequestModel(),
                CreateOrderItemModel(),
                OrderDetailModel(),
                OrderItemModel(),
                OrderMetadataEntryModel(),
                UpdateOrderItemRequestModel(),
                OrderPriorityModel()
            };

        var metadata = reverseInputs
            ? new[]
            {
                EndpointContractMetadata.Create(
                    "PUT",
                    "/orders/{orderId}/items/{itemId}",
                    new EndpointRequestBodyMetadata(new ContractTypeDescriptor(ContractTypeKind.Object, "UpdateOrderItemRequest")),
                    [
                        new EndpointParameterMetadata("itemId", new ContractTypeDescriptor(ContractTypeKind.Guid), false, false),
                        new EndpointParameterMetadata("orderId", new ContractTypeDescriptor(ContractTypeKind.Integer), false, false)
                    ],
                    ["Orders", "Items"],
                    validation: true),
                EndpointContractMetadata.Create(
                    "GET",
                    "/orders/{orderId}",
                    requestBody: null,
                    [
                        new EndpointParameterMetadata("orderId", new ContractTypeDescriptor(ContractTypeKind.Integer), false, false),
                        new EndpointParameterMetadata("includeHistory", new ContractTypeDescriptor(ContractTypeKind.Boolean), true, true)
                    ],
                    ["Reads", "Orders"],
                    validation: false),
                EndpointContractMetadata.Create(
                    "POST",
                    "/orders",
                    new EndpointRequestBodyMetadata(new ContractTypeDescriptor(ContractTypeKind.Object, "CreateOrderRequest")),
                    [],
                    ["Writes", "Orders"],
                    validation: true)
            }
            : new[]
            {
                EndpointContractMetadata.Create(
                    "POST",
                    "/orders",
                    new EndpointRequestBodyMetadata(new ContractTypeDescriptor(ContractTypeKind.Object, "CreateOrderRequest")),
                    [],
                    ["Writes", "Orders"],
                    validation: true),
                EndpointContractMetadata.Create(
                    "GET",
                    "/orders/{orderId}",
                    requestBody: null,
                    [
                        new EndpointParameterMetadata("orderId", new ContractTypeDescriptor(ContractTypeKind.Integer), false, false),
                        new EndpointParameterMetadata("includeHistory", new ContractTypeDescriptor(ContractTypeKind.Boolean), true, true)
                    ],
                    ["Reads", "Orders"],
                    validation: false),
                EndpointContractMetadata.Create(
                    "PUT",
                    "/orders/{orderId}/items/{itemId}",
                    new EndpointRequestBodyMetadata(new ContractTypeDescriptor(ContractTypeKind.Object, "UpdateOrderItemRequest")),
                    [
                        new EndpointParameterMetadata("orderId", new ContractTypeDescriptor(ContractTypeKind.Integer), false, false),
                        new EndpointParameterMetadata("itemId", new ContractTypeDescriptor(ContractTypeKind.Guid), false, false)
                    ],
                    ["Items", "Orders"],
                    validation: true)
            };

        return new ContractManifest(1, endpoints, models, metadata);
    }

    private static EndpointManifestEntry Entry(string id, string method, string route) => new(
        Id: id,
        Feature: "Orders",
        Name: id,
        Method: method,
        Route: route,
        Request: RequestTypeName(id),
        Response: ResponseTypeName(id),
        Authorization: false,
        Permission: null,
        Version: null,
        Tags: [],
        Validation: false,
        Deprecated: false);

    private static string RequestTypeName(string endpointId) => endpointId switch
    {
        "orders.create" => "global::CreateOrderRequest",
        "orders.item.update" => "global::UpdateOrderItemRequest",
        _ => "global::NoRequest"
    };

    private static string ResponseTypeName(string endpointId) => endpointId switch
    {
        "orders.item.update" => "global::OrderItem",
        _ => "global::OrderDetail"
    };

    private static ContractModel CreateOrderRequestModel() => new(
        "CreateOrderRequest",
        "global::CreateOrderRequest",
        [
            new ContractProperty(
                "priority",
                new ContractTypeDescriptor(ContractTypeKind.Enum, "OrderPriority"),
                false,
                false),
            new ContractProperty(
                "notes",
                new ContractTypeDescriptor(ContractTypeKind.String),
                true,
                true),
            new ContractProperty(
                "items",
                new ContractTypeDescriptor(
                    ContractTypeKind.Array,
                    ElementType: new ContractTypeDescriptor(ContractTypeKind.Object, "CreateOrderItem")),
                false,
                false),
            new ContractProperty(
                "customerId",
                new ContractTypeDescriptor(ContractTypeKind.Guid),
                false,
                false),
            new ContractProperty(
                "attributes",
                new ContractTypeDescriptor(
                    ContractTypeKind.Dictionary,
                    ElementType: new ContractTypeDescriptor(ContractTypeKind.String)),
                false,
                false)
        ],
        []);

    private static ContractModel CreateOrderItemModel() => new(
        "CreateOrderItem",
        "global::CreateOrderItem",
        [
            new ContractProperty("quantity", new ContractTypeDescriptor(ContractTypeKind.Integer), false, false),
            new ContractProperty("productCode", new ContractTypeDescriptor(ContractTypeKind.String), false, false)
        ],
        []);

    private static ContractModel OrderDetailModel() => new(
        "OrderDetail",
        "global::OrderDetail",
        [
            new ContractProperty(
                "submittedAt",
                new ContractTypeDescriptor(ContractTypeKind.DateTime),
                false,
                false),
            new ContractProperty(
                "priority",
                new ContractTypeDescriptor(ContractTypeKind.Enum, "OrderPriority"),
                false,
                false),
            new ContractProperty(
                "metadata",
                new ContractTypeDescriptor(
                    ContractTypeKind.Dictionary,
                    ElementType: new ContractTypeDescriptor(ContractTypeKind.Object, "OrderMetadataEntry")),
                false,
                false),
            new ContractProperty(
                "items",
                new ContractTypeDescriptor(
                    ContractTypeKind.Array,
                    ElementType: new ContractTypeDescriptor(ContractTypeKind.Object, "OrderItem")),
                false,
                false),
            new ContractProperty("id", new ContractTypeDescriptor(ContractTypeKind.Integer), false, false),
            new ContractProperty(
                "attributes",
                new ContractTypeDescriptor(
                    ContractTypeKind.Dictionary,
                    ElementType: new ContractTypeDescriptor(ContractTypeKind.String)),
                false,
                false)
        ],
        []);

    private static ContractModel OrderItemModel() => new(
        "OrderItem",
        "global::OrderItem",
        [
            new ContractProperty("quantity", new ContractTypeDescriptor(ContractTypeKind.Integer), false, false),
            new ContractProperty("productCode", new ContractTypeDescriptor(ContractTypeKind.String), false, false),
            new ContractProperty("itemId", new ContractTypeDescriptor(ContractTypeKind.Guid), false, false)
        ],
        []);

    private static ContractModel OrderMetadataEntryModel() => new(
        "OrderMetadataEntry",
        "global::OrderMetadataEntry",
        [
            new ContractProperty("source", new ContractTypeDescriptor(ContractTypeKind.String), false, false),
            new ContractProperty("shiftStart", new ContractTypeDescriptor(ContractTypeKind.TimeOnly), false, false),
            new ContractProperty("capturedOn", new ContractTypeDescriptor(ContractTypeKind.DateOnly), false, false)
        ],
        []);

    private static ContractModel UpdateOrderItemRequestModel() => new(
        "UpdateOrderItemRequest",
        "global::UpdateOrderItemRequest",
        [
            new ContractProperty(
                "quantity",
                new ContractTypeDescriptor(ContractTypeKind.Integer),
                true,
                false),
            new ContractProperty(
                "priority",
                new ContractTypeDescriptor(ContractTypeKind.Enum, "OrderPriority"),
                false,
                false),
            new ContractProperty(
                "metadata",
                new ContractTypeDescriptor(
                    ContractTypeKind.Dictionary,
                    ElementType: new ContractTypeDescriptor(ContractTypeKind.Object, "OrderMetadataEntry")),
                false,
                true)
        ],
        []);

    private static ContractModel OrderPriorityModel() => new(
        "OrderPriority",
        "global::OrderPriority",
        [],
        [
            new ContractEnumValue("Rush", 10),
            new ContractEnumValue("Normal", 0)
        ]);
}
