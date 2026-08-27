using Dotisan.Core;

namespace Dotisan.Core.Tests;

public sealed class EndpointContractTests
{
    [Fact]
    public void Permission_requires_authorization()
    {
        var exception = Assert.Throws<ArgumentException>(() => new EndpointOptions(
            id: "customers.read",
            feature: "Customers",
            name: "ReadCustomers",
            method: "GET",
            route: "/api/customers",
            permission: "customers.read"));

        Assert.Equal("permission", exception.ParamName);
    }

    [Fact]
    public void Authorized_endpoint_accepts_a_permission()
    {
        var options = new EndpointOptions(
            id: "customers.read",
            feature: "Customers",
            name: "ReadCustomers",
            method: "GET",
            route: "/api/customers",
            authorization: true,
            permission: "customers.read");

        Assert.True(options.Authorization);
        Assert.Equal("customers.read", options.Permission);
    }

    [Fact]
    public async Task Endpoint_exposes_deterministic_metadata_and_di_friendly_handler()
    {
        var options = ReadOptions<CreateCustomer>();
        var response = await new CreateCustomer.Handler().HandleAsync(
            new CreateCustomer.Request("Ada"), CancellationToken.None);

        Assert.Equal("customers.create", options.Id);
        Assert.Equal("POST", options.Method);
        Assert.Equal("/api/customers", options.Route);
        Assert.Equal(["Customers", "Writes"], options.Tags);
        Assert.Equal("customers.create", options.Permission);
        Assert.Equal("Ada", response.Name);
    }

    [Fact]
    public async Task Nested_validator_returns_field_level_errors()
    {
        var result = await new CreateCustomer.Validator().ValidateAsync(
            new CreateCustomer.Request(string.Empty), CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(["Name is required."], result.Errors["Name"]);
    }

    private static EndpointOptions ReadOptions<TEndpoint>()
        where TEndpoint : IDotisanEndpoint
    {
        return TEndpoint.Configure();
    }

    private sealed class CreateCustomer : IDotisanEndpoint
    {
        public sealed record Request(string Name);

        public sealed record Response(Guid Id, string Name);

        public sealed class Handler : IDotisanHandler<Request, Response>
        {
            public ValueTask<Response> HandleAsync(Request request, CancellationToken cancellationToken)
            {
                return ValueTask.FromResult(new Response(Guid.Empty, request.Name));
            }
        }

        public sealed class Validator : IDotisanValidator<Request>
        {
            public ValueTask<ValidationResult> ValidateAsync(Request request, CancellationToken cancellationToken)
            {
                var errors = string.IsNullOrWhiteSpace(request.Name)
                    ? new Dictionary<string, string[]> { ["Name"] = ["Name is required."] }
                    : [];
                return ValueTask.FromResult(new ValidationResult(errors));
            }
        }

        public static EndpointOptions Configure() => new(
            id: "customers.create",
            feature: "Customers",
            name: "CreateCustomer",
            method: "POST",
            route: "/api/customers",
            authorization: true,
            permission: "customers.create",
            tags: ["Writes", "Customers"],
            validation: true);
    }
}
