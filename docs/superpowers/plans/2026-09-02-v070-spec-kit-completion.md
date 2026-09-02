# v0.7 Spec Kit Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans (recommended). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the seven unfinished Spec Kit commitments identified after v0.6.

**Architecture:** Preserve the ordinary ASP.NET Core + Vue generated architecture. Add tenant resolution and isolation at the generated application boundary, standardize OpenTelemetry/Aspire development wiring, add frontend test scaffolding and CI execution, provide explicit integration recipes, document EF rollback ownership, and configure shadcn-vue-compatible UI source without adding a runtime framework layer.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core, OpenTelemetry, Aspire Dashboard, Vue 3, Vite, Vitest, Vue Test Utils, Playwright, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-02-v070-spec-kit-completion-design.md`

## Global Constraints

- Generated applications remain ordinary ASP.NET Core and Vue applications.
- Existing v0.6 authentication, authorization, audit, Mailpit, jobs, and scheduling behavior must remain compatible.
- Standard ASP.NET Core, EF Core, OpenTelemetry, Vite, and EF tooling remain the extension boundaries.
- No opaque runtime plugin system is introduced.
- No silent migration creation or application is added to `dotisan new`.
- Deferred notifications, storage, caching, imports/exports, webhooks, and SignalR remain out of scope.

### Task 1: Multi-tenancy resolution and isolation

- [x] Write generated tests for tenant claim/header resolution and production rejection.
- [x] Implement a standard tenant context boundary with authenticated claim precedence and a development-only header fallback.
- [x] Apply required tenant identity consistently to generated resource reads and writes.
- [x] Verify tenancy-disabled output remains unchanged.

### Task 2: Aspire Dashboard development support

- [x] Write CLI/template coverage for observability service configuration and generated Compose output.
- [x] Add an Aspire Dashboard service to the development orchestration path.
- [x] Wire `dotisan dev --observability` to the generated dashboard endpoint using standard OTLP configuration.
- [x] Document ports, startup behavior, and production-provider neutrality.

### Task 3: Complete OpenTelemetry instrumentation

- [x] Write template coverage for EF Core and logging instrumentation.
- [x] Add the standard OpenTelemetry EF Core and logging packages/configuration.
- [x] Verify ASP.NET Core, HTTP, EF, metrics, and logs are configured without enabling export by default.

### Task 4: Generated frontend testing

- [x] Write generator coverage for Vue Test Utils, Playwright, Vitest, and generated smoke tests.
- [x] Add frontend test dependencies/configuration and one deterministic component smoke test.
- [x] Add one Playwright smoke test against the generated frontend shell.
- [x] Run type-check, build, Vitest, and Playwright in CI with explicit service startup.

### Task 5: Integration commands

- [x] Write CLI tests for `dotisan add:integration` and safe provider removal usage.
- [x] Implement explicit recipe registration for supported integrations and configuration placeholders.
- [x] Implement dry-run and refusal behavior for unknown or unsafe removals.
- [x] Document the source changes and review boundaries.

### Task 6: Migration rollback boundary

- [x] Write CLI/documentation coverage for migration status and rollback guidance.
- [x] Keep rollback delegated to `dotnet ef database update <migration>` and document the exact safe workflow.
- [x] Remove contradictory claims from the command index and quickstart.

### Task 7: shadcn-vue setup

- [x] Write generator coverage for shadcn-vue configuration and component ownership.
- [x] Add the required generated configuration/package conventions while keeping components editable.
- [x] Run generated frontend checks and update documentation.

### Task 8: Release verification

- [x] Run the repository test suite and generated API/frontend checks.
- [x] Run the Docker-backed Mailpit smoke path in CI; Aspire Compose output is generated and wired for local use.
- [x] Update the v0.7 checklist and release documentation.
- [x] Rebuild the NuGet package only after all checks pass.
