using Dotisan.Core;
using Dotisan.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Runtime.Loader;

namespace Dotisan.SourceGenerators.Tests;

public sealed class SourceGeneratorTests
{
    [Fact]
    public void Generates_sorted_explicit_mapping_and_manifest()
    {
        const string zEndpoint = """
            public sealed class ZEndpoint : IDotisanEndpoint
            {
                public sealed record Request(string Value);
                public sealed record Response(string Value);
                public sealed class Handler { }
                public sealed class Validator { }
                public static EndpointOptions Configure() => new(
                    "z.write",
                    "Z",
                    "WriteZ",
                    "POST",
                    "/api/z/{id:guid}",
                    tags: ["Writes", "Z"],
                    validation: true);
                public static void Map(IEndpointRouteBuilder endpoints) { }
            }
            """;
        const string aEndpoint = """
            public sealed class AEndpoint : IDotisanEndpoint
            {
                public sealed record Request(
                    [property: JsonPropertyName("display_name")] string DisplayName,
                    int? Count,
                    [property: JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool Enabled,
                    TimeSpan Delay,
                    IReadOnlyList<Nested> Items,
                    Dictionary<string, Status> Lookup);

                public sealed record Response(Nested Item);
                public sealed record Nested(Guid Id, DateOnly Date);
                public enum Status { Unknown = 0, Ready = 2 }
                public sealed class Handler { }
                public sealed class Validator { }
                public static EndpointOptions Configure() => new(
                    "a.read",
                    "A",
                    "ReadA",
                    "GET",
                    "/api/a/{id:guid}",
                    tags: ["Reads", "A"]);
                public static void Map(IEndpointRouteBuilder endpoints) { }
            }
            """;

        var result = RunGenerator(CreateSource(zEndpoint, aEndpoint));
        var reversedResult = RunGenerator(CreateSource(aEndpoint, zEndpoint));

        var generated = GetGeneratedSource(result);
        var reversedGenerated = GetGeneratedSource(reversedResult);
        var contractMembers = GetContractMembers(generated);
        var reversedContractMembers = GetContractMembers(reversedGenerated);

        Assert.True(result.Diagnostics.IsEmpty);
        Assert.True(reversedResult.Diagnostics.IsEmpty);
        Assert.Contains("DotisanEndpointDefinition.For<global::AEndpoint>", generated);
        Assert.Contains("DotisanEndpointDefinition.For<global::ZEndpoint>", generated);
        Assert.True(generated.IndexOf("global::AEndpoint", StringComparison.Ordinal) <
                    generated.IndexOf("global::ZEndpoint", StringComparison.Ordinal));
        Assert.Contains("ToManifestEntry(\"global::AEndpoint.Request\", \"global::AEndpoint.Response\")", generated);
        Assert.Contains("MapDotisanEndpoints(this IEndpointRouteBuilder endpoints)", generated);
        Assert.Contains("AddDotisanEndpointServices(this IServiceCollection services)", generated);
        Assert.Contains("ManifestSchemaVersion", generated);
        Assert.Contains("public static global::System.Collections.Generic.IReadOnlyList<global::Dotisan.Core.EndpointContractMetadata> EndpointMetadata { get; } =", generated);
        Assert.Contains("global::Dotisan.Core.EndpointContractMetadata.Create(", generated);
        Assert.Contains("private static readonly global::Dotisan.Core.EndpointOptions ConfigureSnapshot_AEndpoint = global::AEndpoint.Configure();", generated);
        Assert.Contains("private static readonly global::Dotisan.Core.EndpointOptions ConfigureSnapshot_ZEndpoint = global::ZEndpoint.Configure();", generated);
        Assert.Contains("ConfigureSnapshot_AEndpoint.ToManifestEntry(", generated);
        Assert.Contains("ConfigureSnapshot_AEndpoint.Method", generated);
        Assert.Contains("ConfigureSnapshot_AEndpoint.Route", generated);
        Assert.Contains("ConfigureSnapshot_AEndpoint.Tags", generated);
        Assert.Contains("ConfigureSnapshot_AEndpoint.Validation", generated);
        Assert.DoesNotContain("global::AEndpoint.Configure().Method", generated);
        Assert.Contains("EndpointManifestJson", generated);
        Assert.Contains("EndpointManifestSha256", generated);
        Assert.Contains("public static global::Dotisan.Core.ContractManifest ContractManifest { get; } =", generated);
        Assert.Contains("EndpointMetadata);", generated);
        Assert.Contains("public static string ContractManifestJson => ContractManifest.ToJson();", generated);
        Assert.Contains("public static string ContractManifestSha256 => ContractManifest.Sha256;", generated);
        Assert.Contains("services.AddScoped<global::AEndpoint.Handler>();", generated);
        Assert.Contains("services.AddScoped<global::AEndpoint.Validator>();", generated);
        Assert.Contains("global::Dotisan.Core.ContractTypeKind.Integer", generated);
        Assert.Contains("global::Dotisan.Core.ContractTypeKind.Array", generated);
        Assert.Contains("global::Dotisan.Core.ContractTypeKind.Dictionary", generated);
        Assert.Contains("global::Dotisan.Core.ContractTypeKind.Object", generated);
        Assert.Contains("global::Dotisan.Core.ContractTypeKind.Enum", generated);
        Assert.Contains("global::Dotisan.Core.ContractTypeKind.Unknown", generated);
        Assert.Contains("\"display_name\"", generated);
        Assert.Contains("\"id\"", generated);
        Assert.Contains("\"AEndpointRequest\"", generated);
        Assert.Contains("\"ZEndpointRequest\"", generated);
        Assert.Contains("nullable: true", generated);
        Assert.Contains("optional: true", generated);
        Assert.Contains("\"AEndpointNested\"", generated);
        Assert.Contains("\"AEndpointStatus\"", generated);
        Assert.Contains("new global::Dotisan.Core.ContractEnumValue(\"Unknown\", 0)", generated);
        Assert.Contains("new global::Dotisan.Core.ContractEnumValue(\"Ready\", 2)", generated);
        Assert.Equal(contractMembers, reversedContractMembers);
        Assert.DoesNotContain("Assembly.Load", generated);
        Assert.DoesNotContain("GetTypes(", generated);
        Assert.DoesNotContain("Activator", generated);
    }

    [Fact]
    public void Generated_exports_execute_real_manifest_behavior_and_reuse_one_configure_snapshot_per_endpoint()
    {
        var result = RunGenerator(CreateSource(
            """
            public sealed class SnapshotEndpoint : IDotisanEndpoint
            {
                private static int configureCalls;

                public static int ConfigureCalls => configureCalls;

                public sealed record Request(Guid Id, string Filter);
                public sealed record Response(string Value);

                public static EndpointOptions Configure()
                {
                    configureCalls++;
                    return configureCalls == 1
                        ? new EndpointOptions(
                            "snapshot.read",
                            "Snapshot",
                            "ReadSnapshot",
                            "GET",
                            "/api/snapshot/{id:guid}",
                            tags: ["Snapshot"],
                            validation: true)
                        : new EndpointOptions(
                            "snapshot.changed",
                            "Snapshot",
                            "ChangedSnapshot",
                            "POST",
                            "/api/snapshot",
                            tags: ["Changed"],
                            validation: false);
                }

                public static void Map(IEndpointRouteBuilder endpoints) { }
            }
            """,
            """
            public sealed class PlaceholderEndpoint : IDotisanEndpoint
            {
                public sealed record Request(string Value);
                public sealed record Response(string Value);
                public static EndpointOptions Configure() => new("placeholder.read", "Placeholder", "ReadPlaceholder", "GET", "/api/placeholder");
                public static void Map(IEndpointRouteBuilder endpoints) { }
            }
            """));

        Assert.True(result.Diagnostics.IsEmpty);

        using var generatedAssembly = CompileGeneratedAssembly(CreateSource(
            """
            public sealed class SnapshotEndpoint : IDotisanEndpoint
            {
                private static int configureCalls;

                public static int ConfigureCalls => configureCalls;

                public sealed record Request(Guid Id, string Filter);
                public sealed record Response(string Value);

                public static EndpointOptions Configure()
                {
                    configureCalls++;
                    return configureCalls == 1
                        ? new EndpointOptions(
                            "snapshot.read",
                            "Snapshot",
                            "ReadSnapshot",
                            "GET",
                            "/api/snapshot/{id:guid}",
                            tags: ["Snapshot"],
                            validation: true)
                        : new EndpointOptions(
                            "snapshot.changed",
                            "Snapshot",
                            "ChangedSnapshot",
                            "POST",
                            "/api/snapshot",
                            tags: ["Changed"],
                            validation: false);
                }

                public static void Map(IEndpointRouteBuilder endpoints) { }
            }
            """,
            """
            public sealed class PlaceholderEndpoint : IDotisanEndpoint
            {
                public sealed record Request(string Value);
                public sealed record Response(string Value);
                public static EndpointOptions Configure() => new("placeholder.read", "Placeholder", "ReadPlaceholder", "GET", "/api/placeholder");
                public static void Map(IEndpointRouteBuilder endpoints) { }
            }
            """),
            result);

        var exports = generatedAssembly.Assembly.GetType("Dotisan.Generated.DotisanGeneratedEndpointExtensions");
        var endpointType = generatedAssembly.Assembly.GetType("SnapshotEndpoint");

        Assert.NotNull(exports);
        Assert.NotNull(endpointType);

        var endpointManifest = Assert.IsAssignableFrom<IReadOnlyList<EndpointManifestEntry>>(GetStaticProperty(exports!, "EndpointManifest"));
        var endpointMetadata = Assert.IsAssignableFrom<IReadOnlyList<EndpointContractMetadata>>(GetStaticProperty(exports!, "EndpointMetadata"));
        var contractManifest = Assert.IsType<ContractManifest>(GetStaticProperty(exports!, "ContractManifest"));
        var endpointManifestJson = Assert.IsType<string>(GetStaticProperty(exports!, "EndpointManifestJson"));
        var endpointManifestSha256 = Assert.IsType<string>(GetStaticProperty(exports!, "EndpointManifestSha256"));
        var contractManifestJson = Assert.IsType<string>(GetStaticProperty(exports!, "ContractManifestJson"));
        var contractManifestSha256 = Assert.IsType<string>(GetStaticProperty(exports!, "ContractManifestSha256"));
        var configureCalls = Assert.IsType<int>(GetStaticProperty(endpointType!, "ConfigureCalls"));

        Assert.Equal(new EndpointManifest(endpointManifest).ToJson(), endpointManifestJson);
        Assert.Equal(new EndpointManifest(endpointManifest).Sha256, endpointManifestSha256);
        Assert.Equal(contractManifest.ToJson(), contractManifestJson);
        Assert.Equal(contractManifest.Sha256, contractManifestSha256);
        Assert.Equal(1, configureCalls);
        Assert.Equal("snapshot.read", endpointManifest[1].Id);
        Assert.Equal("GET", endpointManifest[1].Method);
        Assert.Null(endpointMetadata[1].RequestBody);
        Assert.Equal(["id"], endpointMetadata[1].PathParameters.Select(parameter => parameter.Name));
        Assert.Equal(["filter"], endpointMetadata[1].QueryParameters.Select(parameter => parameter.Name));
        Assert.Equal(["Snapshot"], endpointMetadata[1].Tags);
        Assert.True(endpointMetadata[1].Validation.Enabled);
    }

    [Fact]
    public void Ignores_non_record_constructor_parameter_serialization_attributes()
    {
        var result = RunGenerator(CreateSource(
            """
            public sealed class ParameterLeakEndpoint : IDotisanEndpoint
            {
                public sealed class Request
                {
                    public Request(
                        [JsonPropertyName("ctor_name")] string DisplayName,
                        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Enabled)
                    {
                        this.DisplayName = DisplayName;
                        this.Enabled = Enabled;
                    }

                    public string DisplayName { get; }

                    public bool Enabled { get; }
                }

                public sealed record Response(string Value);
                public static EndpointOptions Configure() => new("parameter.leak", "Leak", "ParameterLeak", "POST", "/api/leak");
                public static void Map(IEndpointRouteBuilder endpoints) { }
            }
            """,
            """
            public sealed class PlaceholderEndpoint : IDotisanEndpoint
            {
                public sealed record Request(string Value);
                public sealed record Response(string Value);
                public static EndpointOptions Configure() => new("placeholder.read", "Placeholder", "ReadPlaceholder", "GET", "/api/placeholder");
                public static void Map(IEndpointRouteBuilder endpoints) { }
            }
            """));

        var generated = GetGeneratedSource(result);

        Assert.True(result.Diagnostics.IsEmpty);
        Assert.Contains("\"displayName\"", generated);
        Assert.DoesNotContain("\"ctor_name\"", generated);
        Assert.DoesNotContain("optional: true", generated);
    }

    [Fact]
    public void Includes_inherited_public_properties_without_duplicate_hidden_names()
    {
        var result = RunGenerator(CreateSource(
            """
            public sealed class InheritedEndpoint : IDotisanEndpoint
            {
                public sealed class Request : RequestBase
                {
                    public string Own { get; init; } = string.Empty;
                    public new int Hidden { get; init; }
                }

                public abstract class RequestBase
                {
                    public string BaseValue { get; init; } = string.Empty;
                    public string Hidden { get; init; } = string.Empty;
                }

                public sealed record Response(string Value);
                public static EndpointOptions Configure() => new("inherited.read", "Inherited", "ReadInherited", "GET", "/api/inherited");
                public static void Map(IEndpointRouteBuilder endpoints) { }
            }
            """,
            """
            public sealed class PlaceholderEndpoint : IDotisanEndpoint
            {
                public sealed record Request(string Value);
                public sealed record Response(string Value);
                public static EndpointOptions Configure() => new("placeholder.read", "Placeholder", "ReadPlaceholder", "GET", "/api/placeholder");
                public static void Map(IEndpointRouteBuilder endpoints) { }
            }
            """));

        var generated = GetGeneratedSource(result);

        Assert.True(result.Diagnostics.IsEmpty);
        Assert.Contains("\"baseValue\"", generated);
        Assert.Contains("\"own\"", generated);
        Assert.Equal(2, CountOccurrences(generated, "\"hidden\""));
    }

    [Fact]
    public void Reports_missing_static_map_method()
    {
        var result = RunGenerator(
            """
            using Dotisan.Core;

            public sealed class MissingMapEndpoint : IDotisanEndpoint
            {
                public static EndpointOptions Configure() => new("missing.map", "Missing", "MissingMap", "GET", "/api/missing");
            }
            """);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "DOTISAN001");
    }

    private static GeneratorRunResult RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Append(typeof(IDotisanEndpoint).Assembly)
            .Append(typeof(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder).Assembly);
        var references = assemblies
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .GroupBy(assembly => assembly.Location, StringComparer.OrdinalIgnoreCase)
            .Select(group => MetadataReference.CreateFromFile(group.Key))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            "GeneratorTestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new EndpointRegistrationGenerator());
        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult().Results.Single();
    }

    private static LoadedAssembly CompileGeneratedAssembly(string source, GeneratorRunResult result)
    {
        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(source),
            CSharpSyntaxTree.ParseText(GetGeneratedSource(result))
        };
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Append(typeof(IDotisanEndpoint).Assembly)
            .Append(typeof(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder).Assembly)
            .Append(typeof(Microsoft.Extensions.DependencyInjection.ServiceCollection).Assembly)
            .Append(typeof(Dotisan.AspNetCore.DotisanEndpointRouteBuilderExtensions).Assembly)
            .Append(typeof(System.Text.Json.JsonSerializer).Assembly);
        var references = assemblies
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .GroupBy(assembly => assembly.Location, StringComparer.OrdinalIgnoreCase)
            .Select(group => MetadataReference.CreateFromFile(group.Key))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            "GeneratedBehaviorAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);
        Assert.True(emitResult.Success, string.Join(Environment.NewLine, emitResult.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        peStream.Position = 0;

        var loadContext = new AssemblyLoadContext("GeneratedBehaviorAssemblyContext", isCollectible: true);
        var assembly = loadContext.LoadFromStream(peStream);
        return new LoadedAssembly(loadContext, assembly);
    }

    private static string GetGeneratedSource(GeneratorRunResult result)
    {
        return result.GeneratedSources
            .Single(source => source.HintName == "Dotisan.GeneratedEndpoints.g.cs")
            .SourceText
            .ToString();
    }

    private static object? GetStaticProperty(Type type, string name)
    {
        var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(property);
        return property!.GetValue(null);
    }

    private static string CreateSource(string firstEndpoint, string secondEndpoint)
    {
        return $$"""
            using System;
            using System.Collections.Generic;
            using System.Text.Json.Serialization;
            using Dotisan.Core;
            using Microsoft.AspNetCore.Routing;

            {{firstEndpoint}}

            {{secondEndpoint}}
            """;
    }

    private static string GetContractMembers(string generated)
    {
        const string marker = "    public static global::System.Collections.Generic.IReadOnlyList<global::Dotisan.Core.EndpointContractMetadata> EndpointMetadata { get; } =";
        var index = generated.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(index >= 0, "Expected generated source to contain the EndpointMetadata member.");
        return generated[index..];
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private sealed class LoadedAssembly : IDisposable
    {
        private readonly AssemblyLoadContext loadContext;

        public LoadedAssembly(AssemblyLoadContext loadContext, Assembly assembly)
        {
            this.loadContext = loadContext;
            Assembly = assembly;
        }

        public Assembly Assembly { get; }

        public void Dispose()
        {
            loadContext.Unload();
        }
    }
}
