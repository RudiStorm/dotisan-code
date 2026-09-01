# v0.6 Jobs and Scheduling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Wolverine-backed durable jobs and scheduling foundation to the .NET 10 repository and generated applications.

**Architecture:** Generated ASP.NET Core applications use Wolverine for message execution, durable local queues, retries, scheduled delivery, and transactional outbox persistence. Dotisan owns only editable generated job conventions, provider selection, configuration defaults, and thin CLI inspection commands.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, EF Core 10, WolverineFx, WolverineFx.Sqlite, WolverineFx.EntityFrameworkCore, SQLite, xUnit, generated C# source, existing Dotisan CLI and template renderer.

**Spec:** `docs/superpowers/specs/2026-09-01-jobs-scheduling-design.md`

## Global Constraints

- Generated applications remain ordinary ASP.NET Core applications using standard DI, configuration, logging, EF Core, and hosted services.
- Wolverine is the execution/durability implementation; Dotisan does not create a replacement queue or scheduler.
- Generated job code is editable application source and does not use runtime reflection for registration.
- `dotisan new` never creates or applies database migrations silently.
- .NET and generated projects target `net10.0`.
- Every production behavior change starts with a failing test and is verified with the focused test before proceeding.

### Task 1: Package and options boundary

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/Dotisan.Generators/TemplateFiles.cs`
- Create: `src/Dotisan.Core/Jobs/JobOptions.cs`
- Test: `tests/Dotisan.Core.Tests/JobOptionsTests.cs`

- [x] Add centrally managed Wolverine package versions and generated template package references.
- [x] Add `JobOptions` with `Enabled`, `MaxAttempts`, and `RetryDelaySeconds` defaults and validation.
- [x] Write and run failing options tests.
- [x] Implement the options record and make the tests pass.

### Task 2: Provider-specific Wolverine registration

**Files:**
- Modify: `src/Dotisan.Generators/TemplateFiles.cs`
- Modify: `tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs`

- [x] Add generated `Jobs/JobRegistration.cs` using `UseWolverine`, durable local queues, EF Core transactions, and provider-specific message persistence.
- [x] Emit SQLite, SQL Server, PostgreSQL, and MySQL branches using the existing database selection.
- [x] Add generated appsettings defaults and package assertions.
- [x] Run generator tests red, implement the template, then rerun green.

### Task 3: Editable sample job and dispatch path

**Files:**
- Modify: `src/Dotisan.Generators/TemplateFiles.cs`
- Modify: `src/Dotisan.Generators/TemplateFiles.cs` generated `Program` output
- Test: `tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs`

- [x] Generate `SampleJob`, `SampleJobHandler`, and a minimal endpoint or startup dispatch example using `IMessageBus`.
- [x] Register the generated job assembly explicitly through source code.
- [x] Add assertions for message/handler files and `SendAsync` usage.
- [x] Run focused generator tests and keep all generated code compilable.

### Task 4: Schedule declaration and inspection model

**Files:**
- Create: `src/Dotisan.Core/Jobs/JobSchedule.cs`
- Create: `src/Dotisan.Core/Jobs/JobScheduleReader.cs`
- Test: `tests/Dotisan.Core.Tests/JobScheduleReaderTests.cs`

- [x] Define an immutable schedule descriptor with name, message type, interval, enabled state, and source path.
- [x] Read only explicit generated schedule declarations; do not scan assemblies at runtime.
- [x] Write failing parsing/validation tests, implement the reader, and verify focused tests.

### Task 5: CLI jobs and schedule commands

**Files:**
- Modify: `src/Dotisan.Cli/DotisanApplication.cs`
- Modify: `src/Dotisan.Cli/BuiltInCommands.cs`
- Modify: `src/Dotisan.Cli/DotisanCommandRegistry.cs`
- Test: `tests/Dotisan.Cli.Tests/CliApplicationTests.cs`

- [x] Register `jobs status` and `schedule list` command paths without breaking existing commands.
- [x] Report workspace, API project, provider, enabled state, and schedule source paths.
- [x] Return explicit usage and not-a-Dotisan-project errors.
- [x] Add failing CLI tests, implement, and run the full CLI test suite.

### Task 6: Generated integration tests and persistence setup

**Files:**
- Modify: `src/Dotisan.Generators/TemplateFiles.cs`
- Modify: `tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs`
- Test generated: `tests/<Name>.Api.Tests/JobTests.cs`

- [x] Generate a test-only host that uses SQLite with Wolverine message persistence configured.
- [x] Verify queued sample execution and durable scheduling with real Wolverine handlers.
- [x] Verify disabled schedules do not register their recurring trigger.
- [x] Restore/build/test a generated project and record provider schema limitations.

### Task 7: Documentation and release closeout

**Files:**
- Modify: `README.md`
- Modify: `docs/quickstart.md`
- Create: `docs/jobs-and-scheduling.md`
- Modify: `src/Dotisan.Cli/Dotisan.Cli.csproj`
- Modify: `src/Dotisan.Cli/DotisanApplication.cs`
- Modify: relevant version tests

- [x] Document local SQLite setup, external-provider schema setup, durable retries, scheduling, shutdown behavior, and native Wolverine tooling.
- [x] Align the v0.6 version metadata and release notes.
- [x] Run restore, build, full tests, generated API build/tests, and frontend build.
- [x] Commit with `release: close out v0.6.0`.
