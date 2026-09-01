# Jobs and scheduling

Dotisan 0.6 generates an ordinary ASP.NET Core application with Wolverine for durable local queues, delayed message delivery, bounded retries, and transactional EF Core integration. Wolverine's durable message store uses the same `DefaultConnection` selected for the application.

Generated job source lives under `src/<Name>.Api/Jobs/`:

- `SampleJob.cs` is an editable message contract.
- `SampleJobHandler.cs` is an editable handler using normal dependency injection.
- `JobEndpoints.cs` exposes examples for immediate enqueueing and five-minute delayed scheduling.
- `JobRegistration.cs` configures durable local queues and provider-specific message persistence.

Run these commands from a generated project:

```powershell
dotisan jobs status
dotisan schedule list
```

`dotisan jobs status` reports the selected persistence provider and whether jobs are enabled. `dotisan schedule list` reads explicit `DOTISAN:SCHEDULE` declarations from editable source; it does not scan assemblies or inspect private runtime tables. The generated declaration is an inspectable convention; recurring trigger registration is deferred to a later scheduling slice. The generated endpoint demonstrates one-off delayed delivery through Wolverine.

The default configuration is:

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

Set `Dotisan__Jobs__Enabled=false` through normal ASP.NET Core configuration to disable generated job registration. `MaxAttempts` is applied to the generated Wolverine handler chain. `RetryDelaySeconds` is part of the stable configuration contract and is reserved for the next retry-policy refinement; current Wolverine-native failure handling controls the delay. Failed messages remain available through Wolverine's native error-handling and replay tooling.

For SQLite, the application uses `WolverineFx.Sqlite`. SQL Server, PostgreSQL, and MySQL projects receive the corresponding Wolverine persistence package and registration method. Allow the application or deployment process to provision the Wolverine schema; `dotisan new` never applies application or message-store migrations silently.

Wolverine's local queues and durable scheduling are documented at [Using local queueing](https://wolverinefx.io/guide/messaging/transports/local.html), and its EF Core transaction/outbox integration is documented at [Entity Framework Core integration](https://wolverinefx.io/guide/durability/efcore/).
