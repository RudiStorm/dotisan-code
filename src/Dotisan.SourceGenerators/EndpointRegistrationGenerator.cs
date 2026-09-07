using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dotisan.SourceGenerators;

[Generator]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public sealed class EndpointRegistrationGenerator : IIncrementalGenerator
{
    private const string MarkerName = "Dotisan.Core.IDotisanEndpoint";
    private const string JsonSerializationNamespace = "System.Text.Json.Serialization";
    private const string JsonPropertyNameAttributeTypeName = "JsonPropertyNameAttribute";
    private const string JsonIgnoreAttributeTypeName = "JsonIgnoreAttribute";
    private const string JsonIgnoreConditionTypeName = "JsonIgnoreCondition";
    private static readonly SymbolDisplayFormat FullyQualifiedTypeName = SymbolDisplayFormat.FullyQualifiedFormat;

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
            symbol,
            symbol.ToDisplayString(FullyQualifiedTypeName),
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
        var contractData = BuildContractData(candidates);
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
        foreach (var candidate in candidates)
        {
            source.Append("    private static readonly global::Dotisan.Core.EndpointOptions ")
                .Append(GetConfigureSnapshotFieldName(candidate.TypeName))
                .Append(" = ")
                .Append(candidate.TypeName)
                .AppendLine(".Configure();");
        }

        if (candidates.Count > 0)
        {
            source.AppendLine();
        }

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
                    .Append(GetConfigureSnapshotFieldName(candidate.TypeName))
                    .Append(".ToManifestEntry(\"")
                    .Append(candidate.TypeName)
                    .Append(".Request\", \"")
                    .Append(candidate.TypeName)
                    .AppendLine(".Response\"),");
            }

            source.AppendLine("        };");
        }

        source.AppendLine();
        source.AppendLine("    public const int ManifestSchemaVersion = global::Dotisan.Core.EndpointManifest.SchemaVersion;");
        source.AppendLine("    public static string EndpointManifestJson => new global::Dotisan.Core.EndpointManifest(EndpointManifest).ToJson();");
        source.AppendLine("    public static string EndpointManifestSha256 => new global::Dotisan.Core.EndpointManifest(EndpointManifest).Sha256;");
        source.AppendLine();
        AppendEndpointMetadata(source, contractData.EndpointMetadata);
        AppendContractManifest(source, contractData.Models);
        source.AppendLine("}");
        source.AppendLine();
        source.AppendLine("public static class DotisanContractExport");
        source.AppendLine("{");
        source.AppendLine("    public static string ContractManifestJson => DotisanGeneratedEndpointExtensions.ContractManifestJson;");
        source.AppendLine("}");
        return source.ToString();
    }

    private static ContractBuildResult BuildContractData(IReadOnlyList<EndpointCandidate> candidates)
    {
        var collector = new ContractModelCollector();
        var endpointMetadata = new List<EndpointContractMetadataCandidate>(candidates.Count);

        foreach (var candidate in candidates)
        {
            var request = candidate.Symbol.GetTypeMembers("Request").FirstOrDefault();
            if (request is not null)
            {
                collector.AddRootModel(request, candidate.DisplayName + "Request");
            }

            if (candidate.Symbol.GetTypeMembers("Response").FirstOrDefault() is { } response)
            {
                collector.AddRootModel(response, candidate.DisplayName + "Response");
            }

            endpointMetadata.Add(new EndpointContractMetadataCandidate(
                candidate.TypeName,
                request is null
                    ? null
                    : new ContractTypeDescriptor(ContractTypeKind.Object, ReferenceSourceType: GetSourceTypeName(request)),
                request is null
                    ? []
                    : BuildEndpointRequestParameters(request, collector.AddModel)));
        }

        var modelBuild = collector.Build();

        return new ContractBuildResult(
            modelBuild.Models,
            endpointMetadata
                .Select(candidate => new EmittedEndpointContractMetadata(
                    candidate.EndpointTypeName,
                    candidate.RequestBody is null
                        ? null
                        : new EmittedEndpointRequestBodyMetadata(modelBuild.ResolveDescriptor(candidate.RequestBody)),
                    candidate.RequestParameters
                        .OrderBy(parameter => parameter.Name, StringComparer.Ordinal)
                        .Select(parameter => new EmittedEndpointParameterMetadata(
                            parameter.Name,
                            modelBuild.ResolveDescriptor(parameter.Type),
                            parameter.Nullable,
                            parameter.Optional))
                        .ToArray()))
                .ToArray());
    }

    private static ContractProperty[] BuildEndpointRequestParameters(
        INamedTypeSymbol requestType,
        Action<ContractModel> addModel)
    {
        return GetReadableTransportProperties(requestType)
            .Select(property => new ContractProperty(
                GetTransportPropertyName(property),
                DescribeType(property.Type, GetSourceTypeName(property.Type), addModel),
                IsNullableProperty(property),
                IsOptionalProperty(property)))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AppendEndpointMetadata(
        StringBuilder source,
        IReadOnlyList<EmittedEndpointContractMetadata> endpointMetadata)
    {
        source.AppendLine("    public static global::System.Collections.Generic.IReadOnlyList<global::Dotisan.Core.EndpointContractMetadata> EndpointMetadata { get; } =");
        if (endpointMetadata.Count == 0)
        {
            source.AppendLine("        global::System.Array.Empty<global::Dotisan.Core.EndpointContractMetadata>();");
        }
        else
        {
            source.AppendLine("        new global::Dotisan.Core.EndpointContractMetadata[]");
            source.AppendLine("        {");
            foreach (var metadata in endpointMetadata)
            {
                AppendEndpointMetadataExpression(source, "            ", metadata);
                source.AppendLine(",");
            }

            source.AppendLine("        };");
        }

        source.AppendLine();
    }

    private static void AppendContractManifest(StringBuilder source, IReadOnlyList<EmittedContractModel> models)
    {
        source.AppendLine("    public static global::Dotisan.Core.ContractManifest ContractManifest { get; } =");
        source.AppendLine("        new global::Dotisan.Core.ContractManifest(");
        source.AppendLine("            1,");
        source.AppendLine("            EndpointManifest,");

        if (models.Count == 0)
        {
            source.AppendLine("            global::System.Array.Empty<global::Dotisan.Core.ContractModel>(),");
        }
        else
        {
            source.AppendLine("            new global::Dotisan.Core.ContractModel[]");
            source.AppendLine("            {");
            foreach (var model in models)
            {
                AppendContractModelExpression(source, "                ", model);
                source.AppendLine(",");
            }

            source.AppendLine("            },");
        }

        source.AppendLine("            EndpointMetadata);");
        source.AppendLine();
        source.AppendLine("    public static string ContractManifestJson => ContractManifest.ToJson();");
        source.AppendLine("    public static string ContractManifestSha256 => ContractManifest.Sha256;");
        source.AppendLine();
    }

    private static void AppendEndpointMetadataExpression(
        StringBuilder source,
        string indent,
        EmittedEndpointContractMetadata metadata)
    {
        var configureSnapshotFieldName = GetConfigureSnapshotFieldName(metadata.EndpointTypeName);
        source.Append(indent).AppendLine("global::Dotisan.Core.EndpointContractMetadata.Create(");
        source.Append(indent).Append("    method: ").Append(configureSnapshotFieldName).AppendLine(".Method,");
        source.Append(indent).Append("    route: ").Append(configureSnapshotFieldName).AppendLine(".Route,");
        source.Append(indent).Append("    requestBody: ");
        AppendEndpointRequestBodyExpression(source, metadata.RequestBody);
        source.AppendLine(",");
        source.Append(indent).Append("    requestParameters: ");
        AppendEndpointParameterArray(source, indent + "    ", metadata.RequestParameters);
        source.AppendLine(",");
        source.Append(indent).Append("    tags: ").Append(configureSnapshotFieldName).AppendLine(".Tags,");
        source.Append(indent).Append("    validation: ").Append(configureSnapshotFieldName).Append(".Validation)");
    }

    private static void AppendEndpointRequestBodyExpression(
        StringBuilder source,
        EmittedEndpointRequestBodyMetadata? requestBody)
    {
        if (requestBody is null)
        {
            source.Append("null");
            return;
        }

        source.Append("new global::Dotisan.Core.EndpointRequestBodyMetadata(");
        AppendContractTypeDescriptorExpression(source, requestBody.Type);
        source.Append(')');
    }

    private static void AppendEndpointParameterArray(
        StringBuilder source,
        string indent,
        IReadOnlyList<EmittedEndpointParameterMetadata> parameters)
    {
        if (parameters.Count == 0)
        {
            source.Append("global::System.Array.Empty<global::Dotisan.Core.EndpointParameterMetadata>()");
            return;
        }

        source.AppendLine("new global::Dotisan.Core.EndpointParameterMetadata[]");
        source.Append(indent).AppendLine("{");
        foreach (var parameter in parameters)
        {
            source.Append(indent).AppendLine("    new global::Dotisan.Core.EndpointParameterMetadata(");
            source.Append(indent).Append("        ").Append(ToCSharpStringLiteral(parameter.Name)).AppendLine(",");
            source.Append(indent).Append("        ");
            AppendContractTypeDescriptorExpression(source, parameter.Type);
            source.AppendLine(",");
            source.Append(indent)
                .Append("        /* nullable: ")
                .Append(parameter.Nullable ? "true" : "false")
                .Append(" */ ")
                .Append(parameter.Nullable ? "true" : "false")
                .AppendLine(",");
            source.Append(indent)
                .Append("        /* optional: ")
                .Append(parameter.Optional ? "true" : "false")
                .Append(" */ ")
                .Append(parameter.Optional ? "true" : "false")
                .AppendLine("),");
        }

        source.Append(indent).Append('}');
    }

    private static void AppendContractModelExpression(
        StringBuilder source,
        string indent,
        EmittedContractModel model)
    {
        source.Append(indent).AppendLine("new global::Dotisan.Core.ContractModel(");
        source.Append(indent).Append("    ").Append(ToCSharpStringLiteral(model.Name)).AppendLine(",");
        source.Append(indent).Append("    ").Append(ToCSharpStringLiteral(model.SourceType)).AppendLine(",");
        AppendContractPropertyArray(source, indent + "    ", model.Properties);
        source.AppendLine(",");
        AppendContractEnumValueArray(source, indent + "    ", model.EnumValues);
        source.Append(indent).Append(')');
    }

    private static void AppendContractPropertyArray(
        StringBuilder source,
        string indent,
        IReadOnlyList<EmittedContractProperty> properties)
    {
        if (properties.Count == 0)
        {
            source.Append(indent).Append("global::System.Array.Empty<global::Dotisan.Core.ContractProperty>()");
            return;
        }

        source.Append(indent).AppendLine("new global::Dotisan.Core.ContractProperty[]");
        source.Append(indent).AppendLine("{");
        foreach (var property in properties)
        {
            source.Append(indent).AppendLine("    new global::Dotisan.Core.ContractProperty(");
            source.Append(indent).Append("        ").Append(ToCSharpStringLiteral(property.Name)).AppendLine(",");
            source.Append(indent).Append("        ");
            AppendContractTypeDescriptorExpression(source, property.Type);
            source.AppendLine(",");
            source.Append(indent)
                .Append("        /* nullable: ")
                .Append(property.Nullable ? "true" : "false")
                .Append(" */ ")
                .Append(property.Nullable ? "true" : "false")
                .AppendLine(",");
            source.Append(indent)
                .Append("        /* optional: ")
                .Append(property.Optional ? "true" : "false")
                .Append(" */ ")
                .Append(property.Optional ? "true" : "false")
                .AppendLine("),");
        }

        source.Append(indent).Append('}');
    }

    private static void AppendContractEnumValueArray(
        StringBuilder source,
        string indent,
        IReadOnlyList<ContractEnumValue> enumValues)
    {
        if (enumValues.Count == 0)
        {
            source.Append(indent).Append("global::System.Array.Empty<global::Dotisan.Core.ContractEnumValue>()");
            return;
        }

        source.Append(indent).AppendLine("new global::Dotisan.Core.ContractEnumValue[]");
        source.Append(indent).AppendLine("{");
        foreach (var enumValue in enumValues)
        {
            source.Append(indent).Append("    new global::Dotisan.Core.ContractEnumValue(")
                .Append(ToCSharpStringLiteral(enumValue.Name))
                .Append(", ")
                .Append(enumValue.Value.ToString(CultureInfo.InvariantCulture))
                .AppendLine("),");
        }

        source.Append(indent).Append('}');
    }

    private static void AppendContractTypeDescriptorExpression(
        StringBuilder source,
        EmittedContractTypeDescriptor descriptor)
    {
        source.Append("new global::Dotisan.Core.ContractTypeDescriptor(");
        source.Append("global::Dotisan.Core.ContractTypeKind.").Append(descriptor.Kind);

        if (descriptor.ReferenceName is not null)
        {
            source.Append(", ReferenceName: ").Append(ToCSharpStringLiteral(descriptor.ReferenceName));
        }

        if (descriptor.ElementType is not null)
        {
            source.Append(", ElementType: ");
            AppendContractTypeDescriptorExpression(source, descriptor.ElementType);
        }

        source.Append(')');
    }

    private static ContractTypeDescriptor DescribeType(
        ITypeSymbol type,
        string sourceTypeName,
        Action<ContractModel> addModel)
    {
        var descriptorType = UnwrapNullableType(type);
        sourceTypeName = GetSourceTypeName(descriptorType);

        if (descriptorType.SpecialType == SpecialType.System_String)
        {
            return new ContractTypeDescriptor(ContractTypeKind.String);
        }

        if (descriptorType.SpecialType == SpecialType.System_Boolean)
        {
            return new ContractTypeDescriptor(ContractTypeKind.Boolean);
        }

        if (IsIntegerType(descriptorType.SpecialType))
        {
            return new ContractTypeDescriptor(ContractTypeKind.Integer);
        }

        if (IsDecimalType(descriptorType.SpecialType))
        {
            return new ContractTypeDescriptor(ContractTypeKind.Decimal);
        }

        if (IsSpecialTypeName(descriptorType, "System.Guid"))
        {
            return new ContractTypeDescriptor(ContractTypeKind.Guid);
        }

        if (IsSpecialTypeName(descriptorType, "System.DateTime")
            || IsSpecialTypeName(descriptorType, "System.DateTimeOffset"))
        {
            return new ContractTypeDescriptor(ContractTypeKind.DateTime);
        }

        if (IsSpecialTypeName(descriptorType, "System.DateOnly"))
        {
            return new ContractTypeDescriptor(ContractTypeKind.DateOnly);
        }

        if (IsSpecialTypeName(descriptorType, "System.TimeOnly"))
        {
            return new ContractTypeDescriptor(ContractTypeKind.TimeOnly);
        }

        if (TryGetDictionaryValueType(descriptorType, out var dictionaryValueType))
        {
            return new ContractTypeDescriptor(
                ContractTypeKind.Dictionary,
                ElementType: DescribeType(dictionaryValueType, GetSourceTypeName(dictionaryValueType), addModel));
        }

        if (TryGetEnumerableElementType(descriptorType, out var elementType))
        {
            return new ContractTypeDescriptor(
                ContractTypeKind.Array,
                ElementType: DescribeType(elementType, GetSourceTypeName(elementType), addModel));
        }

        if (descriptorType is INamedTypeSymbol namedType && namedType.TypeKind == TypeKind.Enum)
        {
            addModel(new ContractModel(SanitizeSourceTypeName(sourceTypeName), sourceTypeName, namedType));
            return new ContractTypeDescriptor(ContractTypeKind.Enum, ReferenceSourceType: sourceTypeName);
        }

        if (descriptorType is INamedTypeSymbol objectType && IsObjectType(objectType))
        {
            addModel(new ContractModel(SanitizeSourceTypeName(sourceTypeName), sourceTypeName, objectType));
            return new ContractTypeDescriptor(ContractTypeKind.Object, ReferenceSourceType: sourceTypeName);
        }

        return new ContractTypeDescriptor(ContractTypeKind.Unknown);
    }

    private static ContractModel CreateCompletedModel(
        ContractModel model,
        Action<ContractModel> addModel)
    {
        if (model.Symbol.TypeKind == TypeKind.Enum)
        {
            var enumValues = model.Symbol
                .GetMembers()
                .OfType<IFieldSymbol>()
                .Where(static field => field.HasConstantValue && !field.IsImplicitlyDeclared)
                .Select(field => new ContractEnumValue(field.Name, Convert.ToInt32(field.ConstantValue, CultureInfo.InvariantCulture)))
                .OrderBy(enumValue => enumValue.Name, StringComparer.Ordinal)
                .ToArray();

            return new ContractModel(model.Name, model.SourceType, model.Symbol, [], enumValues);
        }

        var properties = GetReadableTransportProperties(model.Symbol)
            .Select(property => new ContractProperty(
                GetTransportPropertyName(property),
                DescribeType(property.Type, GetSourceTypeName(property.Type), addModel),
                IsNullableProperty(property),
                IsOptionalProperty(property)))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();

        return new ContractModel(model.Name, model.SourceType, model.Symbol, properties, []);
    }

    private static bool IsNullableProperty(IPropertySymbol property)
    {
        return property.NullableAnnotation == NullableAnnotation.Annotated
            || property.Type is INamedTypeSymbol namedType
            && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
    }

    private static bool IsOptionalProperty(IPropertySymbol property)
    {
        foreach (var attribute in GetSerializationAttributes(property))
        {
            if (!IsNamedType(attribute.AttributeClass, JsonIgnoreAttributeTypeName, JsonSerializationNamespace))
            {
                continue;
            }

            foreach (var argument in attribute.NamedArguments)
            {
                if (string.Equals(argument.Key, "Condition", StringComparison.Ordinal)
                    && IsWhenWritingDefault(argument.Value))
                {
                    return true;
                }
            }
        }

        return HasWhenWritingDefaultSyntaxAttribute(property);
    }

    private static bool IsWhenWritingDefault(TypedConstant value)
    {
        var enumType = value.Type;
        if (enumType is null
            || !IsNamedType(enumType, JsonIgnoreConditionTypeName, JsonSerializationNamespace))
        {
            return false;
        }

        var whenWritingDefault = enumType
            .GetMembers("WhenWritingDefault")
            .OfType<IFieldSymbol>()
            .FirstOrDefault(static field => field.HasConstantValue);

        return whenWritingDefault is not null
            && Equals(value.Value, whenWritingDefault.ConstantValue);
    }

    private static string GetTransportPropertyName(IPropertySymbol property)
    {
        foreach (var attribute in GetSerializationAttributes(property))
        {
            if (!IsNamedType(attribute.AttributeClass, JsonPropertyNameAttributeTypeName, JsonSerializationNamespace)
                || attribute.ConstructorArguments.Length == 0
                || attribute.ConstructorArguments[0].Value is not string transportName
                || string.IsNullOrWhiteSpace(transportName))
            {
                continue;
            }

            return transportName;
        }

        if (TryGetJsonPropertyNameFromSyntax(property, out var syntaxTransportName))
        {
            return syntaxTransportName;
        }

        return ConvertToCamelCase(property.Name);
    }

    private static IEnumerable<AttributeData> GetSerializationAttributes(IPropertySymbol property)
    {
        foreach (var attribute in property.GetAttributes())
        {
            yield return attribute;
        }
    }

    private static IPropertySymbol[] GetReadableTransportProperties(INamedTypeSymbol type)
    {
        var properties = new Dictionary<string, IPropertySymbol>(StringComparer.Ordinal);

        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var property in current.GetMembers()
                         .OfType<IPropertySymbol>()
                         .OrderBy(static property => property.Name, StringComparer.Ordinal))
            {
                if (property.IsStatic
                    || property.IsIndexer
                    || property.DeclaredAccessibility != Accessibility.Public
                    || property.GetMethod?.DeclaredAccessibility != Accessibility.Public
                    || properties.ContainsKey(property.Name))
                {
                    continue;
                }

                properties.Add(property.Name, property);
            }
        }

        return properties.Values
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TryGetDictionaryValueType(ITypeSymbol type, out ITypeSymbol valueType)
    {
        if (type is IArrayTypeSymbol)
        {
            valueType = null!;
            return false;
        }

        foreach (var candidate in GetSelfAndInterfaces(type))
        {
            if (!candidate.IsGenericType
                || candidate.TypeArguments.Length != 2
                || candidate.TypeArguments[0].SpecialType != SpecialType.System_String)
            {
                continue;
            }

            if (candidate.ContainingNamespace.ToDisplayString() == "System.Collections.Generic"
                && (candidate.Name == "Dictionary" || candidate.Name == "IDictionary"))
            {
                valueType = candidate.TypeArguments[1];
                return true;
            }
        }

        valueType = null!;
        return false;
    }

    private static bool TryGetEnumerableElementType(ITypeSymbol type, out ITypeSymbol elementType)
    {
        if (type is IArrayTypeSymbol arrayType)
        {
            if (arrayType.Rank == 1)
            {
                elementType = arrayType.ElementType;
                return true;
            }

            elementType = null!;
            return false;
        }

        if (type.SpecialType == SpecialType.System_String)
        {
            elementType = null!;
            return false;
        }

        foreach (var candidate in GetSelfAndInterfaces(type))
        {
            if (candidate.IsGenericType
                && candidate.TypeArguments.Length == 1
                && candidate.Name == "IEnumerable"
                && candidate.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")
            {
                elementType = candidate.TypeArguments[0];
                return true;
            }
        }

        elementType = null!;
        return false;
    }

    private static IEnumerable<INamedTypeSymbol> GetSelfAndInterfaces(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType)
        {
            yield return namedType;

            foreach (var interfaceType in namedType.AllInterfaces.OrderBy(
                         static interfaceSymbol => interfaceSymbol.ToDisplayString(FullyQualifiedTypeName),
                         StringComparer.Ordinal))
            {
                yield return interfaceType;
            }
        }
    }

    private static bool IsObjectType(INamedTypeSymbol type)
    {
        return type.TypeKind == TypeKind.Class || type.IsRecord;
    }

    private static bool IsIntegerType(SpecialType specialType)
    {
        return specialType is SpecialType.System_Byte
            or SpecialType.System_SByte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64;
    }

    private static bool IsDecimalType(SpecialType specialType)
    {
        return specialType is SpecialType.System_Decimal
            or SpecialType.System_Double
            or SpecialType.System_Single;
    }

    private static ITypeSymbol UnwrapNullableType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType
               && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
               && namedType.TypeArguments.Length == 1
            ? namedType.TypeArguments[0]
            : type;
    }

    private static bool IsSpecialTypeName(ITypeSymbol type, string metadataName)
    {
        return string.Equals(type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), metadataName, StringComparison.Ordinal);
    }

    private static bool IsNamedType(ITypeSymbol? type, string name, string containingNamespace)
    {
        return type is INamedTypeSymbol namedType
            && string.Equals(namedType.Name, name, StringComparison.Ordinal)
            && string.Equals(namedType.ContainingNamespace.ToDisplayString(), containingNamespace, StringComparison.Ordinal);
    }

    private static string GetSourceTypeName(ITypeSymbol type)
    {
        return type.ToDisplayString(FullyQualifiedTypeName);
    }

    private static string SanitizeSourceTypeName(string sourceTypeName)
    {
        var builder = new StringBuilder(sourceTypeName.Length);

        foreach (var character in sourceTypeName)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        var sanitized = builder.ToString();

        if (sanitized.StartsWith("global", StringComparison.Ordinal))
        {
            sanitized = sanitized.Substring("global".Length);
        }

        return string.IsNullOrWhiteSpace(sanitized)
            ? "ContractModel"
            : sanitized;
    }

    private static string GetConfigureSnapshotFieldName(string typeName)
    {
        return "ConfigureSnapshot_" + SanitizeSourceTypeName(typeName);
    }

    private static bool TryGetJsonPropertyNameFromSyntax(IPropertySymbol property, out string transportName)
    {
        if (TryGetPrimaryConstructorParameter(property, out var parameterSyntax))
        {
            foreach (var attribute in GetPropertyTargetAttributes(parameterSyntax))
            {
                if (!IsAttributeName(attribute, "JsonPropertyName"))
                {
                    continue;
                }

                if (attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal
                    && literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    transportName = literal.Token.ValueText;
                    return true;
                }
            }
        }

        transportName = string.Empty;
        return false;
    }

    private static bool HasWhenWritingDefaultSyntaxAttribute(IPropertySymbol property)
    {
        if (!TryGetPrimaryConstructorParameter(property, out var parameterSyntax))
        {
            return false;
        }

        foreach (var attribute in GetPropertyTargetAttributes(parameterSyntax))
        {
            if (!IsAttributeName(attribute, "JsonIgnore"))
            {
                continue;
            }

            foreach (var argument in attribute.ArgumentList?.Arguments ?? default)
            {
                if (string.Equals(argument.NameEquals?.Name.Identifier.ValueText, "Condition", StringComparison.Ordinal)
                    && IsWhenWritingDefaultExpression(argument.Expression))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetPrimaryConstructorParameter(IPropertySymbol property, out ParameterSyntax parameterSyntax)
    {
        foreach (var syntaxReference in property.ContainingType.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not RecordDeclarationSyntax recordDeclaration
                || recordDeclaration.ParameterList is null)
            {
                continue;
            }

            foreach (var parameter in recordDeclaration.ParameterList.Parameters)
            {
                if (string.Equals(parameter.Identifier.ValueText, property.Name, StringComparison.Ordinal))
                {
                    parameterSyntax = parameter;
                    return true;
                }
            }
        }

        parameterSyntax = null!;
        return false;
    }

    private static IEnumerable<AttributeSyntax> GetPropertyTargetAttributes(ParameterSyntax parameterSyntax)
    {
        foreach (var attributeList in parameterSyntax.AttributeLists)
        {
            if (attributeList.Target is null
                || !string.Equals(attributeList.Target.Identifier.ValueText, "property", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var attribute in attributeList.Attributes)
            {
                yield return attribute;
            }
        }
    }

    private static bool IsAttributeName(AttributeSyntax attribute, string simpleName)
    {
        var attributeName = attribute.Name.ToString();
        return string.Equals(attributeName, simpleName, StringComparison.Ordinal)
            || string.Equals(attributeName, simpleName + "Attribute", StringComparison.Ordinal)
            || attributeName.EndsWith("." + simpleName, StringComparison.Ordinal)
            || attributeName.EndsWith("." + simpleName + "Attribute", StringComparison.Ordinal);
    }

    private static bool IsWhenWritingDefaultExpression(ExpressionSyntax expression)
    {
        return TryGetRightmostIdentifier(expression, out var identifier)
            && string.Equals(identifier, "WhenWritingDefault", StringComparison.Ordinal);
    }

    private static bool TryGetRightmostIdentifier(ExpressionSyntax expression, out string identifier)
    {
        switch (expression)
        {
            case IdentifierNameSyntax identifierName:
                identifier = identifierName.Identifier.ValueText;
                return true;
            case GenericNameSyntax genericName:
                identifier = genericName.Identifier.ValueText;
                return true;
            case MemberAccessExpressionSyntax memberAccess:
                identifier = memberAccess.Name.Identifier.ValueText;
                return true;
            case QualifiedNameSyntax qualifiedName:
                identifier = qualifiedName.Right.Identifier.ValueText;
                return true;
            case AliasQualifiedNameSyntax aliasQualifiedName:
                identifier = aliasQualifiedName.Name.Identifier.ValueText;
                return true;
            default:
                identifier = string.Empty;
                return false;
        }
    }

    private static string ToCSharpStringLiteral(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');

        foreach (var character in value)
        {
            _ = character switch
            {
                '\\' => builder.Append(@"\\"),
                '"' => builder.Append("\\\""),
                '\r' => builder.Append(@"\r"),
                '\n' => builder.Append(@"\n"),
                '\t' => builder.Append(@"\t"),
                _ => builder.Append(character)
            };
        }

        builder.Append('"');
        return builder.ToString();
    }

    private static string ConvertToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || !char.IsUpper(value[0]))
        {
            return value;
        }

        var characters = value.ToCharArray();
        for (var index = 0; index < characters.Length; index++)
        {
            var hasNext = index + 1 < characters.Length;
            if (index > 0 && hasNext && !char.IsUpper(characters[index + 1]))
            {
                break;
            }

            characters[index] = char.ToLowerInvariant(characters[index]);

            if (!hasNext)
            {
                break;
            }
        }

        return new string(characters);
    }

    private sealed class ContractModelCollector
    {
        private readonly Dictionary<string, ContractModel> modelsBySourceType = new(StringComparer.Ordinal);
        private readonly HashSet<string> building = new(StringComparer.Ordinal);

        public void AddRootModel(INamedTypeSymbol symbol, string name)
        {
            AddModel(new ContractModel(name, GetSourceTypeName(symbol), symbol));
        }

        public void AddModel(ContractModel model)
        {
            if (modelsBySourceType.ContainsKey(model.SourceType) || !building.Add(model.SourceType))
            {
                return;
            }

            var completedModel = CreateCompletedModel(model, AddModel);
            modelsBySourceType[completedModel.SourceType] = completedModel;
            building.Remove(model.SourceType);
        }

        public ContractModelBuildResult Build()
        {
            var ordered = modelsBySourceType.Values
                .OrderBy(model => model.SourceType, StringComparer.Ordinal)
                .ToArray();
            var resolvedNames = ResolveModelNames(ordered);

            var models = ordered
                .Select(model => new EmittedContractModel(
                    resolvedNames[model.SourceType],
                    model.SourceType,
                    model.Properties
                        .OrderBy(property => property.Name, StringComparer.Ordinal)
                        .Select(property => new EmittedContractProperty(
                            property.Name,
                            ResolveDescriptor(property.Type, resolvedNames),
                            property.Nullable,
                            property.Optional))
                        .ToArray(),
                    model.EnumValues
                        .OrderBy(enumValue => enumValue.Name, StringComparer.Ordinal)
                        .ToArray()))
                .OrderBy(model => model.Name, StringComparer.Ordinal)
                .ToArray();

            return new ContractModelBuildResult(models, resolvedNames);
        }

        private static Dictionary<string, string> ResolveModelNames(IReadOnlyList<ContractModel> models)
        {
            var names = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var group in models.GroupBy(model => model.Name, StringComparer.Ordinal))
            {
                var ordered = group
                    .OrderBy(model => model.SourceType, StringComparer.Ordinal)
                    .ToArray();

                for (var index = 0; index < ordered.Length; index++)
                {
                    names[ordered[index].SourceType] = index == 0
                        ? ordered[index].Name
                        : ordered[index].Name + (index + 1).ToString(CultureInfo.InvariantCulture);
                }
            }

            return names;
        }

        private static EmittedContractTypeDescriptor ResolveDescriptor(
            ContractTypeDescriptor descriptor,
            IReadOnlyDictionary<string, string> resolvedNames)
        {
            var referenceName = descriptor.ReferenceSourceType is null
                ? null
                : resolvedNames[descriptor.ReferenceSourceType];
            var elementType = descriptor.ElementType is null
                ? null
                : ResolveDescriptor(descriptor.ElementType, resolvedNames);

            return new EmittedContractTypeDescriptor(descriptor.Kind, referenceName, elementType);
        }
    }

    private sealed class ContractBuildResult
    {
        public ContractBuildResult(
            IReadOnlyList<EmittedContractModel> models,
            IReadOnlyList<EmittedEndpointContractMetadata> endpointMetadata)
        {
            Models = models;
            EndpointMetadata = endpointMetadata;
        }

        public IReadOnlyList<EmittedContractModel> Models { get; }

        public IReadOnlyList<EmittedEndpointContractMetadata> EndpointMetadata { get; }
    }

    private sealed class ContractModelBuildResult
    {
        private readonly IReadOnlyDictionary<string, string> resolvedNames;

        public ContractModelBuildResult(
            IReadOnlyList<EmittedContractModel> models,
            IReadOnlyDictionary<string, string> resolvedNames)
        {
            Models = models;
            this.resolvedNames = resolvedNames;
        }

        public IReadOnlyList<EmittedContractModel> Models { get; }

        public EmittedContractTypeDescriptor ResolveDescriptor(ContractTypeDescriptor descriptor)
        {
            var referenceName = descriptor.ReferenceSourceType is null
                ? null
                : resolvedNames[descriptor.ReferenceSourceType];
            var elementType = descriptor.ElementType is null
                ? null
                : ResolveDescriptor(descriptor.ElementType);

            return new EmittedContractTypeDescriptor(descriptor.Kind, referenceName, elementType);
        }
    }

    private sealed class EndpointContractMetadataCandidate
    {
        public EndpointContractMetadataCandidate(
            string endpointTypeName,
            ContractTypeDescriptor? requestBody,
            IReadOnlyList<ContractProperty> requestParameters)
        {
            EndpointTypeName = endpointTypeName;
            RequestBody = requestBody;
            RequestParameters = requestParameters;
        }

        public string EndpointTypeName { get; }

        public ContractTypeDescriptor? RequestBody { get; }

        public IReadOnlyList<ContractProperty> RequestParameters { get; }
    }

    private sealed class EndpointCandidate
    {
        public EndpointCandidate(
            INamedTypeSymbol symbol,
            string typeName,
            string displayName,
            Location location,
            bool hasConfigure,
            bool hasMap,
            bool hasHandler,
            bool hasValidator)
        {
            Symbol = symbol;
            TypeName = typeName;
            DisplayName = displayName;
            Location = location;
            HasConfigure = hasConfigure;
            HasMap = hasMap;
            HasHandler = hasHandler;
            HasValidator = hasValidator;
        }

        public INamedTypeSymbol Symbol { get; }
        public string TypeName { get; }
        public string DisplayName { get; }
        public Location Location { get; }
        public bool HasConfigure { get; }
        public bool HasMap { get; }
        public bool HasHandler { get; }
        public bool HasValidator { get; }
    }

    private sealed class ContractModel
    {
        public ContractModel(
            string name,
            string sourceType,
            INamedTypeSymbol symbol,
            IReadOnlyList<ContractProperty>? properties = null,
            IReadOnlyList<ContractEnumValue>? enumValues = null)
        {
            Name = name;
            SourceType = sourceType;
            Symbol = symbol;
            Properties = properties ?? [];
            EnumValues = enumValues ?? [];
        }

        public string Name { get; }
        public string SourceType { get; }
        public INamedTypeSymbol Symbol { get; }
        public IReadOnlyList<ContractProperty> Properties { get; }
        public IReadOnlyList<ContractEnumValue> EnumValues { get; }
    }

    private sealed class ContractProperty
    {
        public ContractProperty(string name, ContractTypeDescriptor type, bool nullable, bool optional)
        {
            Name = name;
            Type = type;
            Nullable = nullable;
            Optional = optional;
        }

        public string Name { get; }
        public ContractTypeDescriptor Type { get; }
        public bool Nullable { get; }
        public bool Optional { get; }
    }

    private sealed class ContractEnumValue
    {
        public ContractEnumValue(string name, int value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public int Value { get; }
    }

    private sealed class ContractTypeDescriptor
    {
        public ContractTypeDescriptor(
            ContractTypeKind kind,
            string? ReferenceSourceType = null,
            ContractTypeDescriptor? ElementType = null)
        {
            Kind = kind;
            this.ReferenceSourceType = ReferenceSourceType;
            this.ElementType = ElementType;
        }

        public ContractTypeKind Kind { get; }
        public string? ReferenceSourceType { get; }
        public ContractTypeDescriptor? ElementType { get; }
    }

    private sealed class EmittedContractModel
    {
        public EmittedContractModel(
            string name,
            string sourceType,
            IReadOnlyList<EmittedContractProperty> properties,
            IReadOnlyList<ContractEnumValue> enumValues)
        {
            Name = name;
            SourceType = sourceType;
            Properties = properties;
            EnumValues = enumValues;
        }

        public string Name { get; }
        public string SourceType { get; }
        public IReadOnlyList<EmittedContractProperty> Properties { get; }
        public IReadOnlyList<ContractEnumValue> EnumValues { get; }
    }

    private sealed class EmittedEndpointContractMetadata
    {
        public EmittedEndpointContractMetadata(
            string endpointTypeName,
            EmittedEndpointRequestBodyMetadata? requestBody,
            IReadOnlyList<EmittedEndpointParameterMetadata> requestParameters)
        {
            EndpointTypeName = endpointTypeName;
            RequestBody = requestBody;
            RequestParameters = requestParameters;
        }

        public string EndpointTypeName { get; }

        public EmittedEndpointRequestBodyMetadata? RequestBody { get; }

        public IReadOnlyList<EmittedEndpointParameterMetadata> RequestParameters { get; }
    }

    private sealed class EmittedEndpointRequestBodyMetadata
    {
        public EmittedEndpointRequestBodyMetadata(EmittedContractTypeDescriptor type)
        {
            Type = type;
        }

        public EmittedContractTypeDescriptor Type { get; }
    }

    private sealed class EmittedEndpointParameterMetadata
    {
        public EmittedEndpointParameterMetadata(
            string name,
            EmittedContractTypeDescriptor type,
            bool nullable,
            bool optional)
        {
            Name = name;
            Type = type;
            Nullable = nullable;
            Optional = optional;
        }

        public string Name { get; }

        public EmittedContractTypeDescriptor Type { get; }

        public bool Nullable { get; }

        public bool Optional { get; }
    }

    private sealed class EmittedContractProperty
    {
        public EmittedContractProperty(string name, EmittedContractTypeDescriptor type, bool nullable, bool optional)
        {
            Name = name;
            Type = type;
            Nullable = nullable;
            Optional = optional;
        }

        public string Name { get; }
        public EmittedContractTypeDescriptor Type { get; }
        public bool Nullable { get; }
        public bool Optional { get; }
    }

    private sealed class EmittedContractTypeDescriptor
    {
        public EmittedContractTypeDescriptor(
            ContractTypeKind kind,
            string? referenceName = null,
            EmittedContractTypeDescriptor? elementType = null)
        {
            Kind = kind;
            ReferenceName = referenceName;
            ElementType = elementType;
        }

        public ContractTypeKind Kind { get; }
        public string? ReferenceName { get; }
        public EmittedContractTypeDescriptor? ElementType { get; }
    }

    private enum ContractTypeKind
    {
        String,
        Boolean,
        Integer,
        Decimal,
        Guid,
        DateTime,
        DateOnly,
        TimeOnly,
        Array,
        Dictionary,
        Object,
        Enum,
        Unknown
    }
}
