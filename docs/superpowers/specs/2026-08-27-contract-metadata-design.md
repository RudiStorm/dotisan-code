# v0.5 Compile-Time Contract Metadata Design

## Goal

Extend the existing Roslyn endpoint generator so the endpoint manifest also carries deterministic request and response contract shapes that the TypeScript generator can consume without runtime endpoint discovery.

## Scope

This first v0.5 slice includes:

- typed contract metadata in `Dotisan.Core`;
- compile-time extraction of nested endpoint `Request` and `Response` properties by `Dotisan.SourceGenerators`;
- deterministic serialization of the contract metadata alongside the existing endpoint manifest;
- primitive TypeScript rendering in `Dotisan.TypeScript` for strings, booleans, integers, decimals, GUIDs, UTC date/time values, `DateOnly`, `TimeOnly`, arrays, nullable values, optional values, dictionaries, nested objects, and numeric enums;
- unit tests proving deterministic ordering, nullability, optionality, and enum values;
- generated source that remains inspectable in normal compiler output.

The source generator remains the authority for discovery. The TypeScript package consumes typed metadata and does not inspect assemblies, load application code, or perform reflection.

## Explicitly out of scope

The following are later v0.5 slices:

- OpenAPI document emission;
- Zod schema generation;
- generated `fetch` services;
- TanStack Query composables;
- automatic integration into the golden Vue template;
- a CLI command and stale-output check;
- FluentValidation rule extraction;
- UI or CRUD scaffolding.

These depend on the stable contract metadata shape but should not be bundled into this foundational change.

## Architecture

The source generator continues to discover classes implementing `IDotisanEndpoint`. For every valid endpoint it resolves the nested `Request` and `Response` types and emits a deterministic `ContractManifest` object together with the current `EndpointManifest` object.

The generated metadata uses ordinary immutable records from `Dotisan.Core`:

```text
ContractManifest
├── SchemaVersion
├── Endpoints
└── Models
    └── ContractModel
        └── ContractProperty
            └── ContractTypeDescriptor
```

The descriptor represents transport shape rather than CLR implementation details. Property names use the ASP.NET Core JSON camel-case convention. Nullable and optional are separate flags. Arrays, dictionaries, nested models, and enums retain their element/reference information instead of being reduced to `unknown`.

The TypeScript renderer accepts a `ContractManifest` instance and returns deterministic files in memory. It does not write to disk in this slice. A later CLI integration can choose how to write and validate those files without changing the metadata or rendering contracts.

## Contract metadata

The stable v0.5 model is:

```csharp
public sealed record ContractManifest(
    int SchemaVersion,
    IReadOnlyList<EndpointManifestEntry> Endpoints,
    IReadOnlyList<ContractModel> Models);

public sealed record ContractModel(
    string Name,
    string SourceType,
    IReadOnlyList<ContractProperty> Properties,
    IReadOnlyList<ContractEnumValue> EnumValues);

public sealed record ContractProperty(
    string Name,
    ContractTypeDescriptor Type,
    bool Nullable,
    bool Optional);

public sealed record ContractEnumValue(string Name, int Value);
```

The exact type descriptor is:

```csharp
public enum ContractTypeKind
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

public sealed record ContractTypeDescriptor(
    ContractTypeKind Kind,
    string? ReferenceName = null,
    ContractTypeDescriptor? ElementType = null);
```

`ElementType` is the array element type or dictionary value type. `ReferenceName` is the `ContractModel.Name` for object and enum descriptors. Supported serialized kinds are `string`, `boolean`, `integer`, `decimal`, `guid`, `dateTime`, `dateOnly`, `timeOnly`, `array`, `dictionary`, `object`, `enum`, and `unknown`.

The source generator names a request model `<EndpointClassName>Request` and a response model `<EndpointClassName>Response`; `SourceType` retains the fully qualified C# nested type name for traceability. Nested object and enum models use a deterministic identifier derived from their fully qualified C# type name. If two source types would produce the same identifier, the generator appends a stable ordinal suffix based on ordinal source-name ordering.

Properties use the `[JsonPropertyName]` value when present; otherwise the generator applies the ASP.NET Core `System.Text.Json` camel-case convention. `Nullable` comes from the Roslyn nullable annotation or nullable value type. `Optional` is true only when the property has `JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)`; nullable and optional therefore remain distinct.

Models and properties are sorted ordinally before serialization. The manifest JSON uses camel-case property names, never includes assembly-qualified names, and produces the same SHA-256 hash for the same source metadata regardless of compilation input order.

## TypeScript output contract

The renderer emits one deterministic `models.ts` text result for the manifest. Model names remain valid PascalCase TypeScript identifiers; transport property names remain camelCase. Mappings are:

```text
string       -> string
boolean      -> boolean
integer      -> number
decimal      -> number
guid         -> string
dateTime     -> string
dateOnly     -> string
timeOnly     -> string
array        -> T[]
dictionary   -> Record<string, T>
object       -> referenced model name
enum         -> numeric enum declaration
unknown      -> unknown
```

`Nullable` appends `| null` and `Optional` appends `| undefined`; both flags may be true and remain distinct. Numeric enum values are emitted explicitly.

## Error handling

Unsupported CLR shapes are represented as `unknown` metadata and produce a deterministic renderer result. They do not silently map to an incorrect primitive. The source generator continues to report existing endpoint shape diagnostics. Missing nested request/response types are represented by no contract model for that endpoint and do not introduce runtime reflection.

## Testing strategy

- Core tests verify descriptor validation, deterministic model/property ordering, JSON shape, and hash stability.
- Source-generator tests verify request/response property extraction, JSON naming, nullable/optional flags, arrays, dictionaries, nested objects, and numeric enum values.
- TypeScript tests verify every supported mapping and deterministic model output.
- Existing endpoint registration, manifest, CLI, generator, and generated-project tests remain unchanged and must continue to pass.

## Constitution alignment

- ASP.NET Core, EF Core, DI, and configuration are unchanged.
- Discovery remains compile-time and generated output is inspectable.
- The endpoint manifest remains the canonical API metadata model.
- TypeScript consumes backend-generated metadata rather than defining a second contract.
- No runtime plugin, custom ORM, custom DI container, or reflection-based endpoint registry is introduced.
