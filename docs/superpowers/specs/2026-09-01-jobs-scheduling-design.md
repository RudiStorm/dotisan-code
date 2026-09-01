# Dotisan v0.6 Jobs and Scheduling Design

## Goal

Add a durable background-work foundation to generated .NET 10 applications using Wolverine for message execution, retries, durable local queues, and transactional outbox persistence, while Dotisan owns only the application-facing conventions and scheduler configuration.

## Scope

The v0.6 slice includes:

- Wolverine integration in generated API projects.
- SQLite-backed durable message storage by default.
- Provider-aware durable storage configuration for SQL Server, PostgreSQL, and MySQL.
- Standard ASP.NET Core DI, configuration, logging, and hosted-service startup.
- Editable job message and handler scaffolding.
- One-off delayed scheduling and recurring schedule registration.
- Retry and failure policies with clear development logging.
- Transactional outbox enrollment for EF Core handlers.
- CLI commands for job status and scheduler inspection.
- Generated-project tests for job dispatch, retry behavior, persistence, and schedule registration.

The slice does not include a custom dashboard, distributed leader election, a new queue protocol, or replacement abstractions for `IHostedService`, `IConfiguration`, `DbContext`, or Wolverine’s message bus.

## Architecture

Generated applications register Wolverine through the normal ASP.NET Core host. Wolverine owns message routing, worker execution, retries, durable inbox/outbox storage, and scheduled message delivery. Dotisan adds a small, editable `Jobs` folder containing message contracts, handlers, and schedule registration code that uses `IMessageBus` and Wolverine’s scheduling APIs.

SQLite is the default local durable store. The selected database provider controls the Wolverine message persistence extension and uses the same configured `DefaultConnection` as the application. Developers author and apply Wolverine schema migrations through the normal provider tooling; `dotisan new` never applies migrations silently.

## Generated application contract

Generated projects include:

```text
src/<Name>.Api/Jobs/
├── JobRegistration.cs
├── SampleJob.cs
└── SampleJobHandler.cs
```

`JobRegistration` is ordinary source code and registers:

- Wolverine with durable local queues.
- EF Core transactional middleware.
- The selected message persistence provider.
- The generated handler assembly.

The sample job is intentionally small and editable. It demonstrates an `IMessageBus.SendAsync` call from application code and a handler that receives normal DI services.

## Configuration

Generated `appsettings.json` contains a `Dotisan:Jobs` section:

```json
{
  "Dotisan": {
    "Jobs": {
      "Enabled": true,
      "MaxAttempts": 3,
      "RetryDelaySeconds": 5
    }
  }
}
```

The application may override these values with standard ASP.NET Core configuration. `Enabled=false` prevents generated schedule registration but does not remove Wolverine from the service container.

## CLI contract

The v0.6 CLI adds:

- `dotisan jobs status` — reports whether the workspace contains a jobs-enabled API and prints the configured persistence provider.
- `dotisan schedule list` — lists schedules declared by generated source and reports the source files to edit.

Commands return a helpful generated-project error when run outside a Dotisan workspace. They do not inspect private Wolverine runtime tables or introduce a custom administration protocol.

## Failure handling

- Handler failures are retried up to `MaxAttempts`.
- After retries are exhausted, Wolverine’s durable error handling keeps the failed message available for inspection/replay through native Wolverine tooling.
- Cancellation during host shutdown is treated as normal termination and does not convert into a job failure.
- Missing database connection strings and unsupported provider configuration fail during startup with the existing standard ASP.NET Core exception path.

## Testing and acceptance

The slice is complete when:

1. The repository restores and builds on .NET 10 with zero warnings/errors.
2. Unit tests cover job options, schedule discovery, CLI status/list behavior, and provider selection.
3. Generated SQLite applications restore, build, run the API tests, and execute a queued sample job.
4. A generated application can register a delayed message and retain it across a process restart when the message store schema exists.
5. Generated frontend installation and build behavior remains unchanged.
6. Documentation explains provider schema setup, retry behavior, scheduling, and native Wolverine tooling.

