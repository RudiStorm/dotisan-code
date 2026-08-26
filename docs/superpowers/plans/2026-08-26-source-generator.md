# Source Generator Implementation Plan

> **For agentic workers:** Use the source-generator plan task-by-task with tests before implementation.

Goal: add deterministic compile-time endpoint discovery and generated explicit registration.

Architecture: Dotisan.SourceGenerators targets netstandard2.0 and consumes endpoint syntax plus semantic symbols. It does not depend on runtime reflection or execute application code. The generated extension calls Dotisan.AspNetCore typed definitions and exposes a deterministic Core manifest.

Scope:

- [x] Discover classes implementing Dotisan.Core.IDotisanEndpoint.
- [x] Require static Configure and Map members.
- [x] Emit DOTISAN001 when Map is absent and DOTISAN002 when Configure is absent.
- [x] Sort endpoints by fully qualified name before generation.
- [x] Emit explicit mapping and manifest source that remains inspectable.

Verification:

- [x] Focused Roslyn tests must pass.
- [x] Full solution build and tests must pass.
- [x] The source-generator checkpoint is committed separately from the foundation and endpoint-contract checkpoints.
