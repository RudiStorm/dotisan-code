# v0.7 Spec Kit Completion Checklist

This checklist tracks the seven gaps identified in the Spec Kit audit after v0.6.

- [x] 1. Complete multi-tenancy resolution, generated CRUD tenant predicates, and tenant-context security tests.
- [x] 2. Add Aspire Dashboard development Compose support and `dotisan dev --observability` startup.
- [x] 3. Add OpenTelemetry EF Core and logging instrumentation alongside existing ASP.NET Core/HTTP instrumentation.
- [x] 4. Add generated Vue Test Utils, Vitest/jsdom, Playwright, and frontend CI execution.
- [x] 5. Add explicit `dotisan add:integration` and `dotisan remove:integration` recipe commands.
- [x] 6. Document the standard EF Core migration rollback boundary and remove contradictory migration claims.
- [x] 7. Add shadcn-vue-compatible configuration, Tailwind/PostCSS setup, and editable utility conventions.

## Verification

- Repository tests: passing.
- Fresh generated API: build passing.
- Fresh generated Vue frontend: build and Vitest passing.
- Fresh generated Playwright smoke: passing.
- Multi-tenant generated API suite: passing, including claim/header precedence and production header rejection.
