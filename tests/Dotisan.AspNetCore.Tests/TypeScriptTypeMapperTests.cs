using Dotisan.Core;
using Dotisan.TypeScript;

namespace Dotisan.AspNetCore.Tests;

public sealed class TypeScriptTypeMapperTests
{
    private static readonly string[] GeneratedFileNames = ["models.ts", "schemas.ts", "services.ts", "queries.ts"];
    private static readonly string[] StableFileNames = ["models.ts", "queries.ts"];

    [Fact]
    public void Generates_models_schemas_services_and_queries_for_a_contract_manifest()
    {
        var manifest = new ContractManifest(
            1,
            [new EndpointManifestEntry("profiles.list", "Profiles", "ListProfiles", "GET", "/api/profiles", "void", "Profile[]", false, null, "v1", ["Profiles"], false, false)],
            [new ContractModel(
                "Profile",
                "global::Profile",
                [new ContractProperty("name", new ContractTypeDescriptor(ContractTypeKind.String), false, false)],
                [])],
            [new EndpointContractMetadata(
                null,
                [],
                [],
                200,
                ["Profiles"],
                new EndpointValidationMetadata(false, []))]);

        var files = TypeScriptContractGenerator.GenerateAll(manifest);

        Assert.Equal(GeneratedFileNames, files.Select(file => file.Path).ToArray());
        Assert.Contains("export const ProfileSchema = z.object", files.Single(file => file.Path == "schemas.ts").Content);
        Assert.Contains("export async function listProfiles", files.Single(file => file.Path == "services.ts").Content);
        Assert.Contains("useListProfilesQuery", files.Single(file => file.Path == "queries.ts").Content);
        Assert.Contains("import type { Profile } from \"./models\";", files.Single(file => file.Path == "services.ts").Content);
    }

    [Fact]
    public void Generated_mutation_hooks_accept_only_the_request_body()
    {
        var manifest = new ContractManifest(
            1,
            [new EndpointManifestEntry("profiles.create", "Profiles", "CreateProfile", "POST", "/api/profiles", "Profile", "void", false, null, null, ["Profiles"], true, false)],
            [new ContractModel("Profile", "global::Profile", [new ContractProperty("name", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], [])],
            [new EndpointContractMetadata(null, [], [], 201, ["Profiles"], new EndpointValidationMetadata(true, []))]);

        var output = TypeScriptContractGenerator.GenerateAll(manifest).Single(file => file.Path == "queries.ts").Content;
        var services = TypeScriptContractGenerator.GenerateAll(manifest).Single(file => file.Path == "services.ts").Content;

        Assert.Contains("mutationFn: (body: Parameters<typeof services.createProfile>[0]) => services.createProfile(body)", output);
        Assert.Contains("headers: { \"Content-Type\": \"application/json\" },", services);
        Assert.Contains("...(init.body ? { \"Content-Type\": \"application/json\" } : {}), ...init.headers", services);
    }

    [Fact]
    public void Generated_services_close_urls_with_path_parameters()
    {
        var manifest = new ContractManifest(
            1,
            [new EndpointManifestEntry("sessions.revoke", "Sessions", "RevokeSession", "DELETE", "/api/sessions/{id}", "void", "void", true, "authenticated", null, ["Sessions"], false, false)],
            [],
            [new EndpointContractMetadata(null, [new EndpointParameterMetadata("id", new ContractTypeDescriptor(ContractTypeKind.Guid), false, false)], [], 204, ["Sessions"], new EndpointValidationMetadata(false, []))]);

        var services = TypeScriptContractGenerator.GenerateAll(manifest).Single(file => file.Path == "services.ts").Content;

        Assert.Contains("`/api/sessions/${encodeURIComponent(String(id))}`", services);
    }

    [Fact]
    public void Generated_services_expose_friendly_validation_messages_and_field_errors()
    {
        var manifest = new ContractManifest(
            1,
            [new EndpointManifestEntry("account.register", "Account", "Register", "POST", "/api/account/register", "RegisterRequest", "void", false, null, null, ["Account"], true, false)],
            [],
            [new EndpointContractMetadata(null, [], [], 204, ["Account"], new EndpointValidationMetadata(true, []))]);

        var services = TypeScriptContractGenerator.GenerateAll(manifest).Single(file => file.Path == "services.ts").Content;

        Assert.Contains("export type ApiFieldErrors = Record<string, string[]>;", services);
        Assert.Contains("fieldErrors: ApiFieldErrors", services);
        Assert.Contains("Please fix the highlighted fields.", services);
        Assert.Contains("if (status === 401) return \"Invalid email or password.\"", services);
        Assert.True(services.IndexOf("super(getErrorMessage", StringComparison.Ordinal) < services.IndexOf("this.correlationId", StringComparison.Ordinal));
    }

    [Fact]
    public void Generated_file_manifest_hash_is_stable_for_file_order()
    {
        var files = new[]
        {
            new GeneratedTypeScriptFile("queries.ts", "queries\n"),
            new GeneratedTypeScriptFile("models.ts", "models\n")
        };

        var first = GeneratedFileManifest.Create(files);
        var second = GeneratedFileManifest.Create(files.AsEnumerable().Reverse().ToArray());

        Assert.Equal(StableFileNames, first.Files.Select(file => file.Path).ToArray());
        Assert.Equal(first.Sha256, second.Sha256);
    }

    [Fact]
    public void Zod_schemas_render_portable_validation_rules_and_optional_fields()
    {
        var manifest = new ContractManifest(
            1,
            [new EndpointManifestEntry("users.create", "Users", "CreateUser", "POST", "/api/users", "User", "User", false, null, null, [], true, false)],
            [new ContractModel(
                "User",
                "global::User",
                [
                    new ContractProperty("email", new ContractTypeDescriptor(ContractTypeKind.String), false, false),
                    new ContractProperty("nickname", new ContractTypeDescriptor(ContractTypeKind.String), true, true)
                ],
                [])],
            [new EndpointContractMetadata(
                null,
                [],
                [],
                201,
                [],
                new EndpointValidationMetadata(true,
                [
                    new EndpointValidationRuleMetadata("email", "email"),
                    new EndpointValidationRuleMetadata("email", "length", "3"),
                    new EndpointValidationRuleMetadata("nickname", "pattern", "^[a-z]+$")
                ]))]);

        var output = ZodSchemaGenerator.Generate(manifest).Content;

        Assert.Contains("email: z.string().email().min(3)", output);
        Assert.Contains("nickname: z.string().regex(/^[a-z]+$/).nullable().optional()", output);
    }

    [Fact]
    public void Zod_schemas_reject_unknown_validation_rules()
    {
        var manifest = new ContractManifest(
            1,
            [new EndpointManifestEntry("users.create", "Users", "CreateUser", "POST", "/api/users", "User", "User", false, null, null, [], true, false)],
            [new ContractModel("User", "global::User", [new ContractProperty("email", new ContractTypeDescriptor(ContractTypeKind.String), false, false)], [])],
            [new EndpointContractMetadata(null, [], [], 201, [], new EndpointValidationMetadata(true, [new EndpointValidationRuleMetadata("email", "custom")]))]);

        var exception = Assert.Throws<InvalidOperationException>(() => ZodSchemaGenerator.Generate(manifest));

        Assert.Contains("custom", exception.Message);
        Assert.Contains("FluentValidation", exception.Message);
    }

    [Theory]
    [InlineData(ContractTypeKind.String, "string")]
    [InlineData(ContractTypeKind.Boolean, "boolean")]
    [InlineData(ContractTypeKind.Integer, "number")]
    [InlineData(ContractTypeKind.Decimal, "number")]
    [InlineData(ContractTypeKind.Guid, "string")]
    [InlineData(ContractTypeKind.DateTime, "string")]
    [InlineData(ContractTypeKind.DateOnly, "string")]
    [InlineData(ContractTypeKind.TimeOnly, "string")]
    [InlineData(ContractTypeKind.Unknown, "unknown")]
    public void Maps_primitive_contract_kinds(ContractTypeKind kind, string expected)
        => Assert.Equal(expected, TypeScriptTypeMapper.Map(new ContractTypeDescriptor(kind)));

    [Fact]
    public void Maps_recursive_descriptors()
    {
        Assert.Equal(
            "number[]",
            TypeScriptTypeMapper.Map(new ContractTypeDescriptor(
                ContractTypeKind.Array,
                ElementType: new ContractTypeDescriptor(ContractTypeKind.Integer))));

        Assert.Equal(
            "Record<string, string>",
            TypeScriptTypeMapper.Map(new ContractTypeDescriptor(
                ContractTypeKind.Dictionary,
                ElementType: new ContractTypeDescriptor(ContractTypeKind.String))));

        Assert.Equal(
            "Profile",
            TypeScriptTypeMapper.Map(new ContractTypeDescriptor(ContractTypeKind.Object, "Profile")));
    }

    [Fact]
    public void Applies_nullable_before_optional_union_members()
    {
        Assert.Equal(
            "string | null | undefined",
            TypeScriptTypeMapper.Map(
                new ContractTypeDescriptor(ContractTypeKind.String),
                nullable: true,
                optional: true));
    }

    [Fact]
    public void Maps_contract_type_compatibility_overload_with_flags()
    {
        Assert.Equal("string | null", TypeScriptTypeMapper.Map(ContractType.String, nullable: true));
        Assert.Equal("number | undefined", TypeScriptTypeMapper.Map(ContractType.Decimal, optional: true));
    }

    [Fact]
    public void Renders_empty_manifest_as_newline_terminated_models_file()
    {
        var generated = TypeScriptContractGenerator.Generate(new ContractManifest(1, [], []));

        Assert.Single(generated);
        Assert.Equal("models.ts", generated[0].Path);
        Assert.Equal(Environment.NewLine, generated[0].Content);
    }

    [Fact]
    public void Renders_models_ts_deterministically()
    {
        var reversed = new ContractManifest(
            1,
            [],
            [
                new ContractModel(
                    "Request",
                    "global::Request",
                    [
                        new ContractProperty("profile", new ContractTypeDescriptor(ContractTypeKind.Object, "Profile"), false, true),
                        new ContractProperty("displayName", new ContractTypeDescriptor(ContractTypeKind.String), false, false),
                        new ContractProperty("attributes", new ContractTypeDescriptor(
                            ContractTypeKind.Dictionary,
                            ElementType: new ContractTypeDescriptor(ContractTypeKind.String)), false, false),
                        new ContractProperty("status", new ContractTypeDescriptor(ContractTypeKind.Enum, "Status"), true, true),
                        new ContractProperty("tags", new ContractTypeDescriptor(
                            ContractTypeKind.Array,
                            ElementType: new ContractTypeDescriptor(ContractTypeKind.Integer)), false, false)
                    ],
                    []),
                new ContractModel(
                    "Profile",
                    "global::Profile",
                    [
                        new ContractProperty("identifier", new ContractTypeDescriptor(ContractTypeKind.Guid), false, false)
                    ],
                    []),
                new ContractModel(
                    "Status",
                    "global::Status",
                    [],
                    [
                        new ContractEnumValue("Ready", 2),
                        new ContractEnumValue("Unknown", 0)
                    ])
            ]);

        var expected = string.Join(
            Environment.NewLine,
            [
                "export enum Status {",
                "  Ready = 2,",
                "  Unknown = 0,",
                "}",
                "",
                "export interface Profile {",
                "  identifier: string;",
                "}",
                "",
                "export interface Request {",
                "  attributes: Record<string, string>;",
                "  displayName: string;",
                "  profile: Profile | undefined;",
                "  status: Status | null | undefined;",
                "  tags: number[];",
                "}"
            ]) + Environment.NewLine;

        var generated = TypeScriptContractGenerator.Generate(reversed);
        var regenerated = TypeScriptContractGenerator.Generate(new ContractManifest(
            1,
            [],
            [
                new ContractModel(
                    "Status",
                    "global::Status",
                    [],
                    [
                        new ContractEnumValue("Unknown", 0),
                        new ContractEnumValue("Ready", 2)
                    ]),
                new ContractModel(
                    "Profile",
                    "global::Profile",
                    [
                        new ContractProperty("identifier", new ContractTypeDescriptor(ContractTypeKind.Guid), false, false)
                    ],
                    []),
                new ContractModel(
                    "Request",
                    "global::Request",
                    [
                        new ContractProperty("tags", new ContractTypeDescriptor(
                            ContractTypeKind.Array,
                            ElementType: new ContractTypeDescriptor(ContractTypeKind.Integer)), false, false),
                        new ContractProperty("status", new ContractTypeDescriptor(ContractTypeKind.Enum, "Status"), true, true),
                        new ContractProperty("attributes", new ContractTypeDescriptor(
                            ContractTypeKind.Dictionary,
                            ElementType: new ContractTypeDescriptor(ContractTypeKind.String)), false, false),
                        new ContractProperty("displayName", new ContractTypeDescriptor(ContractTypeKind.String), false, false),
                        new ContractProperty("profile", new ContractTypeDescriptor(ContractTypeKind.Object, "Profile"), false, true)
                    ],
                    [])
            ]));

        Assert.Single(generated);
        Assert.Equal("models.ts", generated[0].Path);
        Assert.Equal(expected, generated[0].Content);
        Assert.Equal(generated[0].Content, regenerated[0].Content);
    }

    [Fact]
    public void Renders_enum_members_in_manifest_name_order_instead_of_numeric_value_order()
    {
        var manifest = new ContractManifest(
            1,
            [],
            [
                new ContractModel(
                    "Priority",
                    "global::Priority",
                    [],
                    [
                        new ContractEnumValue("Beta", 10),
                        new ContractEnumValue("Alpha", 20)
                    ])
            ]);

        var generated = TypeScriptContractGenerator.Generate(manifest);

        var expected = string.Join(
            Environment.NewLine,
            [
                "export enum Priority {",
                "  Alpha = 20,",
                "  Beta = 10,",
                "}"
            ]) + Environment.NewLine;

        Assert.Single(generated);
        Assert.Equal(expected, generated[0].Content);
    }
}
