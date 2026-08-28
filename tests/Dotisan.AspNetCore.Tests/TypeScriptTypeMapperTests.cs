using Dotisan.Core;
using Dotisan.TypeScript;

namespace Dotisan.AspNetCore.Tests;

public sealed class TypeScriptTypeMapperTests
{
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
