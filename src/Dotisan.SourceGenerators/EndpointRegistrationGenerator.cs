using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dotisan.SourceGenerators;

[Generator]
public sealed class EndpointRegistrationGenerator : IIncrementalGenerator
{
    private const string MarkerName = "Dotisan.Core.IDotisanEndpoint";

    private static readonly DiagnosticDescriptor MissingMap = new(
        "DOTISAN001",
        "Endpoint is missing static Map",
        "Endpoint '{0}' must declare a public static Map(IEndpointRouteBuilder) method",
        "Dotisan",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingConfigure = new(
        "DOTISAN002",
        "Endpoint is missing static Configure",
        "Endpoint '{0}' must declare a public static Configure() method",
        "Dotisan",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var endpoints = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => IsCandidateSyntax(node),
                static (syntaxContext, _) => GetCandidate(syntaxContext))
            .Where(static candidate => candidate is not null)
            .Select(static (candidate, _) => candidate!)
            .Collect();

        context.RegisterSourceOutput(endpoints, static (sourceProductionContext, candidates) =>
            Emit(sourceProductionContext, candidates));
    }

    private static bool IsCandidateSyntax(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDeclaration
            && classDeclaration.BaseList is not null
            && classDeclaration.BaseList.Types.Count > 0;
    }

    private static EndpointCandidate? GetCandidate(GeneratorSyntaxContext context)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not INamedTypeSymbol symbol
            || !symbol.AllInterfaces.Any(interfaceSymbol =>
                string.Equals(interfaceSymbol.ToDisplayString(), MarkerName, StringComparison.Ordinal)))
        {
            return null;
        }

        return new EndpointCandidate(
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            symbol.Name,
            context.Node.GetLocation(),
            HasStaticMethod(symbol, "Configure", parameterCount: 0),
            HasStaticMethod(symbol, "Map", parameterCount: 1),
            symbol.GetTypeMembers("Handler").Length > 0,
            symbol.GetTypeMembers("Validator").Length > 0);
    }

    private static bool HasStaticMethod(INamedTypeSymbol symbol, string name, int parameterCount)
    {
        return symbol.GetMembers(name)
            .OfType<IMethodSymbol>()
            .Any(method => method.IsStatic && method.Parameters.Length == parameterCount);
    }

    private static void Emit(
        SourceProductionContext context,
        IReadOnlyCollection<EndpointCandidate> candidates)
    {
        var ordered = candidates
            .GroupBy(candidate => candidate.TypeName, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(candidate => candidate.TypeName, StringComparer.Ordinal)
            .ToArray();

        foreach (var candidate in ordered)
        {
            if (!candidate.HasMap)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MissingMap,
                    candidate.Location,
                    candidate.DisplayName));
            }

            if (!candidate.HasConfigure)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MissingConfigure,
                    candidate.Location,
                    candidate.DisplayName));
            }
        }

        var valid = ordered
            .Where(candidate => candidate.HasMap && candidate.HasConfigure)
            .ToArray();

        context.AddSource(
            "Dotisan.GeneratedEndpoints.g.cs",
            Microsoft.CodeAnalysis.Text.SourceText.From(
                BuildSource(valid),
                Encoding.UTF8));
    }

    private static string BuildSource(IReadOnlyList<EndpointCandidate> candidates)
    {
        var source = new StringBuilder();
        source.AppendLine("using System;");
        source.AppendLine("using System.Collections.Generic;");
        source.AppendLine("using Microsoft.AspNetCore.Routing;");
        source.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        source.AppendLine("using Dotisan.AspNetCore;");
        source.AppendLine("using Dotisan.Core;");
        source.AppendLine();
        source.AppendLine("namespace Dotisan.Generated;");
        source.AppendLine();
        source.AppendLine("public static class DotisanGeneratedEndpointExtensions");
        source.AppendLine("{");
        source.AppendLine("    public static IEndpointRouteBuilder MapDotisanEndpoints(this IEndpointRouteBuilder endpoints)");
        source.AppendLine("    {");

        if (candidates.Count == 0)
        {
            source.AppendLine("        return endpoints;");
        }
        else
        {
            source.AppendLine("        return DotisanEndpointRouteBuilderExtensions.MapDotisanEndpoints(");
            source.AppendLine("            endpoints,");
            source.AppendLine("            new DotisanEndpointDefinition[]");
            source.AppendLine("            {");
            foreach (var candidate in candidates)
            {
                source.Append("                DotisanEndpointDefinition.For<")
                    .Append(candidate.TypeName)
                    .Append(">(static routes => ")
                    .Append(candidate.TypeName)
                    .AppendLine(".Map(routes)),");
            }

            source.AppendLine("            });");
        }

        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    public static IServiceCollection AddDotisanEndpointServices(this IServiceCollection services)");
        source.AppendLine("    {");
        foreach (var candidate in candidates)
        {
            if (candidate.HasHandler)
            {
                source.Append("        services.AddScoped<")
                    .Append(candidate.TypeName)
                    .AppendLine(".Handler>();");
            }

            if (candidate.HasValidator)
            {
                source.Append("        services.AddScoped<")
                    .Append(candidate.TypeName)
                    .AppendLine(".Validator>();");
            }
        }

        source.AppendLine("        return services;");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    public static IReadOnlyList<EndpointManifestEntry> EndpointManifest { get; } =");
        if (candidates.Count == 0)
        {
            source.AppendLine("        Array.Empty<EndpointManifestEntry>();");
        }
        else
        {
            source.AppendLine("        new EndpointManifestEntry[]");
            source.AppendLine("        {");
            foreach (var candidate in candidates)
            {
                source.Append("            ")
                    .Append(candidate.TypeName)
                    .Append(".Configure().ToManifestEntry(\"")
                    .Append(candidate.TypeName)
                    .Append(".Request\", \"")
                    .Append(candidate.TypeName)
                    .AppendLine(".Response\"),");
            }

            source.AppendLine("        };");
        }

        source.AppendLine("}");
        return source.ToString();
    }

    private sealed class EndpointCandidate
    {
        public EndpointCandidate(
            string typeName,
            string displayName,
            Location location,
            bool hasConfigure,
            bool hasMap,
            bool hasHandler,
            bool hasValidator)
        {
            TypeName = typeName;
            DisplayName = displayName;
            Location = location;
            HasConfigure = hasConfigure;
            HasMap = hasMap;
            HasHandler = hasHandler;
            HasValidator = hasValidator;
        }

        public string TypeName { get; }
        public string DisplayName { get; }
        public Location Location { get; }
        public bool HasConfigure { get; }
        public bool HasMap { get; }
        public bool HasHandler { get; }
        public bool HasValidator { get; }
    }
}
