using Dotisan.Core;
using Dotisan.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dotisan.SourceGenerators.Tests;

public sealed class SourceGeneratorTests
{
    [Fact]
    public void Generates_sorted_explicit_mapping_and_manifest()
    {
        var result = RunGenerator(
            """
            using Dotisan.Core;
            using Microsoft.AspNetCore.Routing;

            public sealed class ZEndpoint : IDotisanEndpoint
            {
                public sealed record Request(string Value);
                public sealed record Response(string Value);
                public sealed class Handler { }
                public sealed class Validator { }
                public static EndpointOptions Configure() => new("z.read", "Z", "ReadZ", "GET", "/api/z");
                public static void Map(IEndpointRouteBuilder endpoints) { }
            }

            public sealed class AEndpoint : IDotisanEndpoint
            {
                public sealed record Request(string Value);
                public sealed record Response(string Value);
                public sealed class Handler { }
                public sealed class Validator { }
                public static EndpointOptions Configure() => new("a.read", "A", "ReadA", "GET", "/api/a");
                public static void Map(IEndpointRouteBuilder endpoints) { }
            }
            """);

        var generated = GetGeneratedSource(result);

        Assert.True(result.Diagnostics.IsEmpty);
        Assert.Contains("DotisanEndpointDefinition.For<global::AEndpoint>", generated);
        Assert.Contains("DotisanEndpointDefinition.For<global::ZEndpoint>", generated);
        Assert.True(generated.IndexOf("global::AEndpoint", StringComparison.Ordinal) <
                    generated.IndexOf("global::ZEndpoint", StringComparison.Ordinal));
        Assert.Contains("ToManifestEntry(\"global::AEndpoint.Request\", \"global::AEndpoint.Response\")", generated);
        Assert.Contains("MapDotisanEndpoints(this IEndpointRouteBuilder endpoints)", generated);
        Assert.Contains("AddDotisanEndpointServices(this IServiceCollection services)", generated);
        Assert.Contains("ManifestSchemaVersion", generated);
        Assert.Contains("EndpointManifestJson", generated);
        Assert.Contains("EndpointManifestSha256", generated);
        Assert.Contains("services.AddScoped<global::AEndpoint.Handler>();", generated);
        Assert.Contains("services.AddScoped<global::AEndpoint.Validator>();", generated);
        Assert.DoesNotContain("Assembly.Load", generated);
        Assert.DoesNotContain("GetTypes(", generated);
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

    private static string GetGeneratedSource(GeneratorRunResult result)
    {
        return result.GeneratedSources
            .Single(source => source.HintName == "Dotisan.GeneratedEndpoints.g.cs")
            .SourceText
            .ToString();
    }
}
