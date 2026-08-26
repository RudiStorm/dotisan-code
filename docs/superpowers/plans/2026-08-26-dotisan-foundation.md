# Dotisan Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first usable Dotisan slice: a testable CLI foundation and `dotisan new` generator that creates a buildable ASP.NET Core + Vue/Vite application with SQLite defaults.

**Architecture:** `Dotisan.Cli` owns argument dispatch, prompts, console output, exit codes, and command registration. `Dotisan.Core` owns stable contracts and endpoint metadata; `Dotisan.AspNetCore` provides conventional explicit endpoint mapping; `Dotisan.Generators` owns the inspectable golden template; `Dotisan.TypeScript` owns deterministic backend-to-TypeScript mapping primitives; and `Dotisan.Testing` contains shared test helpers. The generated app remains an ordinary ASP.NET Core app with normal configuration, EF Core SQLite, and a separate Vite frontend.

**Tech Stack:** .NET 8, ASP.NET Core Minimal APIs, EF Core SQLite, C# records/interfaces, xUnit, Vue 3, TypeScript, Vite, npm/pnpm-compatible package scripts, GitHub Actions.

**Spec:** `Dotisan Spec Kit.md`

## Global Constraints

- Preserve standard ASP.NET Core dependency injection, configuration, middleware, and EF Core behavior.
- Keep endpoint discovery compile-time-oriented and generated code inspectable; do not add runtime reflection discovery.
- Use one golden application architecture and keep `dotisan.config` limited to orchestration settings.
- Keep the first slice focused on repository skeleton, CLI foundation, `dotisan new`, and generated-project smoke validation.
- Later commands must report explicit helpful errors and a stable non-success exit code.

### Task 1: Repository Skeleton

- [ ] Create the solution and focused projects for CLI, Core, ASP.NET integration, generators, TypeScript generation, testing, and tests.
- [ ] Add central build properties, package versions, editor rules, ignore rules, and CI.
- [ ] Add the Core contract tests before production behavior.

### Task 2: CLI Foundation

- [ ] Add failing tests for version/help, command registration, prompts, exit codes, and later-command errors.
- [ ] Implement minimal interfaces and command dispatch to make those tests pass.
- [ ] Keep console and prompt dependencies injectable for deterministic tests.

### Task 3: Golden Template and New Wizard

- [ ] Add failing generator tests for project name validation, generated files, SQLite defaults, and config choices.
- [ ] Implement a deterministic template generator with inspectable source files.
- [ ] Implement Quick wizard defaults plus non-interactive flags and `dotisan new` dispatch.

### Task 4: Integration, Documentation, and Verification

- [ ] Add explicit ASP.NET endpoint mapping and TypeScript generator tests.
- [ ] Add README and quickstart documentation for local CLI execution and generated apps.
- [ ] Run `dotnet test`, `dotnet build`, and a generated-project build smoke test; record exact dependency or environment blockers.
