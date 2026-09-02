# v0.7 Production Readiness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move Dotisan from the v0.6 feature foundation toward a production-ready v0.7 by making current behavior truthful, adding actionable diagnostics, and covering generated applications in CI.

**Architecture:** Keep generated applications as ordinary ASP.NET Core projects. Add `doctor` as a read-only CLI inspection layer over existing workspace services and generated source conventions; do not add a replacement configuration or runtime plugin system. Implement broader batteries (tenancy, observability, notifications, storage, cache, imports, exports, and webhooks) as separate follow-up slices after the production-readiness baseline is green.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core, Vue/Vite, xUnit, GitHub Actions, existing Dotisan CLI service abstractions.

**Spec:** `Dotisan Spec Kit.md`, `docs/superpowers/specs/2026-09-01-jobs-scheduling-design.md`, and the v0.6.2 repository behavior.

## Global Constraints

- Generated applications remain recognizable ASP.NET Core applications.
- Production commands must never create migrations silently.
- `dotisan new` may create and apply the initial development migration only when restore/setup is enabled.
- Diagnostics are read-only and must report PASS, WARNING, or BLOCKING results.
- Every behavior change starts with a failing test and is verified with focused tests before broader verification.
- Existing source-generator and CLI conventions remain the implementation boundary.

### Task 1: Documentation consistency and roadmap

**Files:**
- Modify: `docs/jobs-and-scheduling.md`
- Modify: `docs/quickstart.md`
- Modify: `project.md`
- Create: `docs/v0.7-roadmap.md`

- [x] Update all migration instructions to distinguish automatic development initialization from explicit production migration application.
- [x] Replace obsolete command descriptions with the current v0.6.2 command names and options.
- [x] Record deferred features and acceptance criteria for each v0.7 slice.
- [x] Search for contradictory claims and run `git diff --check`.

### Task 2: `dotisan doctor` production diagnostics

**Files:**
- Create: `src/Dotisan.Core/Diagnostics/DiagnosticResult.cs`
- Create: `src/Dotisan.Core/Diagnostics/DiagnosticReport.cs`
- Create: `src/Dotisan.Cli/Diagnostics/DoctorService.cs`
- Modify: `src/Dotisan.Cli/BuiltInCommands.cs`
- Modify: `src/Dotisan.Cli/DotisanApplication.cs`
- Test: `tests/Dotisan.Core.Tests/DiagnosticReportTests.cs`
- Test: `tests/Dotisan.Cli.Tests/DoctorCommandTests.cs`

- [x] Add failing tests for pass, warning, blocking results, exit codes, missing workspace, missing API project, and production-only checks.
- [x] Implement immutable diagnostic records with stable ordering and `PASS`, `WARNING`, and `BLOCKING` output.
- [x] Implement read-only checks for workspace structure, migrations, production Dockerfile, health endpoint, frontend, and provider configuration.
- [x] Register `doctor` and support `doctor --production` with usage errors for unknown options.
- [x] Run the focused Core and CLI tests.

### Task 3: Generated-project CI smoke coverage

**Files:**
- Modify: `.github/workflows/ci.yml`
- Create or modify: `tests/Dotisan.Cli.Tests/GeneratedProjectSmokeTests.cs`
- Modify: `src/Dotisan.Generators/TemplateFiles.cs` only where smoke failures identify a real template defect.

- [x] Add CI coverage that generates a plain API smoke project and builds/tests its generated API solution.
- [x] Keep network-dependent restore explicit and report failures without hiding the source error.
- [x] Pack the CLI in CI and upload the `.nupkg` as a workflow artifact.
- [x] Verify the workflow YAML shape and run the equivalent local commands.

### Task 4: Multi-tenancy foundation

**Files:**
- Create: `src/Dotisan.Core/Tenancy/TenantContext.cs`
- Modify: `src/Dotisan.Generators/TemplateFiles.cs`
- Modify: `src/Dotisan.Cli/BuiltInCommands.cs`
- Test: `tests/Dotisan.Core.Tests/TenantContextTests.cs`
- Test: generated API integration tests in `src/Dotisan.Generators/TemplateFiles.cs`

- [ ] Add explicit tenant resolution from a standard claim/header boundary.
- [ ] Generate tenant-aware context registration and a required tenant identifier for enabled projects.
- [ ] Add EF query/write isolation tests proving one tenant cannot read or modify another tenant's data.
- [ ] Keep disabled tenancy output identical to the existing non-tenant shape.

### Task 5: Observability baseline

**Files:**
- Modify: `src/Dotisan.Generators/TemplateFiles.cs`
- Modify: `README.md`
- Modify: `docs/quickstart.md`
- Test: `tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs`

- [x] Add standard OpenTelemetry traces, metrics, and ASP.NET Core/HTTP instrumentation to generated applications.
- [x] Add configurable OTLP/Aspire development export without hard-coding production destinations.
- [x] Add correlation/trace guidance and verify health endpoint telemetry wiring.
- [x] Add generated-template assertions and run API build/tests.

### Task 6: Authentication completion and integrations

**Files:**
- Modify: `src/Dotisan.Generators/TemplateFiles.cs`
- Modify: `README.md`
- Modify: `docs/quickstart.md`
- Test: generated authentication integration tests

- [ ] Implement email confirmation, password reset, TOTP MFA, recovery codes, and session management using standard ASP.NET Identity.
- [ ] Add explicit extension points for SMTP/Mailpit and external providers.
- [ ] Add security tests for token expiry, replay, lockout, MFA enrollment, and recovery-code use.
- [ ] Keep unsupported integrations out of the generated app until configured.

### Task 7: Release closeout

**Files:**
- Modify: version metadata and tests
- Modify: `README.md`, `docs/quickstart.md`, `docs/v0.7-roadmap.md`
- Modify: `.github/workflows/ci.yml`

- [ ] Run restore, Release build, all tests, generated-project smoke tests, frontend checks, package creation, and `git diff --check`.
- [ ] Review the complete change list and update release notes.
- [ ] Create a v0.7 package only after the implemented scope is genuinely complete.
