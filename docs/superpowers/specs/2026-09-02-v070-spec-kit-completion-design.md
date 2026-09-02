# v0.7 Spec Kit Completion Design

## Goal

Complete the seven remaining Spec Kit gaps identified after v0.6: multi-tenancy isolation, Aspire development support, OpenTelemetry completeness, generated frontend testing, integration commands, migration rollback guidance, and proper shadcn-vue setup.

## Scope and boundaries

The work keeps generated applications as ordinary ASP.NET Core and Vue applications. Existing v0.6 authentication, authorization, audit, Mailpit, jobs, and scheduling behavior remains unchanged. Notifications, storage, caching, imports/exports, webhooks, and SignalR remain deferred roadmap features.

## Architecture

Multi-tenancy will use a provider-neutral tenant context resolved from a standard authenticated claim with an explicit development header fallback. Generated resource handlers will continue to apply tenant predicates and required tenant assignment, with integration tests proving read and write isolation.

Observability will remain standard OpenTelemetry. Generated apps will add EF Core and logging instrumentation, while `dotisan dev --observability` starts an Aspire Dashboard container and configures OTLP through normal environment variables. No production vendor is selected.

Frontend testing will add Vue Test Utils and Playwright dependencies/configuration, generate smoke tests, and run both type-check/build and frontend tests in CI. Integration commands will be explicit source/configuration recipes, not a runtime plugin system.

Migration rollback will remain standard EF Core tooling rather than inventing a second migration engine; Dotisan will provide a clear status/rollback guidance boundary. shadcn-vue will be configured through generated source and package conventions while preserving editable generated UI code.

## Acceptance criteria

1. Enabled tenancy has a resolvable tenant identity and generated CRUD integration tests reject cross-tenant reads and writes.
2. `dotisan dev --observability` starts a documented Aspire Dashboard service and the API exports OTLP to it.
3. Generated apps include ASP.NET Core, EF Core, HTTP, and logging OpenTelemetry instrumentation.
4. Generated Vue apps pass type-check, build, Vitest, and Playwright smoke checks in CI.
5. `dotisan add:integration` and provider removal have explicit usage, dry-run/safety behavior, and tests.
6. README and quickstart clearly document that migration rollback uses `dotnet ef` and how to perform it safely.
7. Generated Vue projects have an explicit shadcn-vue-compatible setup and documentation.
