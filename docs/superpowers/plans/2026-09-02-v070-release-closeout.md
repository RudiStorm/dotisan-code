# v0.7.0 Release Closeout Implementation Plan

> **For agentic workers:** Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish the verified v0.7 foundation as the Dotisan 0.7.0 release artifact.

**Architecture:** Update the single CLI version source and generated frontend/package documentation references together. Rebuild the existing unsigned NuGet artifact, then verify repository tests, build, package metadata, and working-tree hygiene.

**Tech Stack:** .NET 10, NuGet, generated ASP.NET Core/Vue templates, xUnit, Git.

## Global Constraints

- Keep the existing feature behavior unchanged.
- Keep NuGet unsigned unless a signing certificate and publishing policy are supplied.
- Use `0.7.0` consistently in CLI metadata, generated frontend metadata, documentation, and package metadata.
- Do not include stale or unused package references in generated projects.

### Task 1: Version metadata and documentation

- [x] Update the CLI project and generated frontend version references from `0.6.6` to `0.7.0`.
- [x] Update install, update, and signing documentation to describe v0.7.0.
- [x] Update version assertions and run formatting checks.

### Task 2: Package and verification

- [x] Restore and run the full Release test suite.
- [x] Build the solution with zero warnings and errors.
- [x] Pack `Dotisan.0.7.0.nupkg` and verify its metadata.
- [x] Run `git diff --check`, inspect the final diff, commit, and push `master`.
