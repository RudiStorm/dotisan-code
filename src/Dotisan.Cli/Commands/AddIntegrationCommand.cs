using Dotisan.Core;

namespace Dotisan.Cli;

internal sealed class AddIntegrationCommand : WorkspaceCommand
{
    public override string Name => "add:integration";
    public override string Description => "Add an explicit integration recipe to the workspace.";

    public override async Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var dryRun = arguments.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
        var names = arguments.Where(argument => !argument.Equals("--dry-run", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (names.Length != 1) return Fail(context.Console, "Usage: dotisan add:integration <aspire|sendgrid|mailgun|signoz|notifications|storage|caching|imports-exports|webhooks> [--dry-run].", DotisanExitCode.UsageError);
        if (!IntegrationRecipes.TryGetValue(names[0], out var recipe)) return Fail(context.Console, $"Unknown integration '{names[0]}'. Supported integrations: {string.Join(", ", IntegrationRecipes.Keys)}.", DotisanExitCode.UsageError);
        if (!TryGetServices(context, out var services) || services.SolutionPath is null) return Fail(context.Console, "Could not find a generated Dotisan project. Run this command from the project root.");
        var integrationDirectory = Path.Combine(services.WorkingDirectory, ".dotisan", "integrations");
        var path = Path.Combine(integrationDirectory, names[0].ToLowerInvariant() + ".md");
        if (dryRun) { context.Console.WriteLine($"Would create {Path.GetRelativePath(services.WorkingDirectory, path)}"); context.Console.WriteLine(recipe); return DotisanExitCode.Success; }
        Directory.CreateDirectory(integrationDirectory);
        await File.WriteAllTextAsync(path, recipe, cancellationToken);
        context.Console.WriteLine($"Created {Path.GetRelativePath(services.WorkingDirectory, path)}. Apply the documented package and configuration changes explicitly.");
        return DotisanExitCode.Success;
    }

    private static readonly IReadOnlyDictionary<string, string> IntegrationRecipes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["aspire"] = "# Aspire Dashboard\n\nUse `dotisan dev --observability`. Configure OTLP through `OTEL_EXPORTER_OTLP_ENDPOINT`.\n",
        ["sendgrid"] = "# SendGrid\n\nAdd a provider adapter implementing `IEmailProvider`. Configure `SendGrid:ApiKey` and `SendGrid:From` through deployment secrets.\n",
        ["mailgun"] = "# Mailgun\n\nAdd a provider adapter implementing `IEmailProvider`. Configure `Mailgun:ApiKey`, `Mailgun:Domain`, and `Mailgun:From` through deployment secrets.\n",
        ["signoz"] = "# SigNoz\n\nSet `OTEL_EXPORTER_OTLP_ENDPOINT` to the SigNoz collector endpoint and enable export with `OpenTelemetry:Enabled=true`.\n",
        ["notifications"] = "# Notifications\n\nGenerate with `dotisan new <Name> --notifications yes`. Readiness: development-adapter. Add retention, pagination, and delivery monitoring before production use.\n",
        ["storage"] = "# File storage\n\nGenerate with `dotisan new <Name> --storage yes`. Readiness: example-only. Add durable metadata/ownership and implement a reviewed provider for multi-node production.\n",
        ["caching"] = "# Caching\n\nGenerate with `dotisan new <Name> --caching yes`. Readiness: development-adapter. Configure a distributed provider for multi-instance production.\n",
        ["imports-exports"] = "# Imports and exports\n\nGenerate with `dotisan new <Name> --imports-exports yes`. Readiness: development-adapter. Generated source includes bounded CSV/JSON uploads, EF-backed status, and a queued handler; add durable payload storage, domain processing, retention, and monitoring before production use.\n",
        ["webhooks"] = "# Webhooks\n\nGenerate with `dotisan new <Name> --webhooks yes`. Readiness: development-adapter. Generated source includes HMAC signing, URL validation, timeout, durable background dispatch when jobs are enabled, retries, delivery history, fresh replay signatures, and endpoint authorization; add destination policy, secret rotation, and observability before production use.\n"
    };
}
