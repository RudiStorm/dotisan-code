using Dotisan.Core;

namespace Dotisan.Cli;

internal sealed class RemoveIntegrationCommand : WorkspaceCommand
{
    public override string Name => "remove:integration";
    public override string Description => "Remove an explicit integration recipe from the workspace.";

    public override async Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var force = arguments.Contains("--force", StringComparer.OrdinalIgnoreCase);
        var names = arguments.Where(argument => !argument.Equals("--force", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (names.Length != 1) return Fail(context.Console, "Usage: dotisan remove:integration <name> --force.", DotisanExitCode.UsageError);
        if (!force) return Fail(context.Console, "Refusing to remove an integration without --force. Review the generated source changes first.", DotisanExitCode.UsageError);
        if (!TryGetServices(context, out var services) || services.SolutionPath is null) return Fail(context.Console, "Could not find a generated Dotisan project. Run this command from the project root.");
        var path = Path.Combine(services.WorkingDirectory, ".dotisan", "integrations", names[0].ToLowerInvariant() + ".md");
        if (!File.Exists(path)) return Fail(context.Console, $"Integration recipe '{names[0]}' was not found.", DotisanExitCode.UsageError);
        File.Delete(path);
        context.Console.WriteLine($"Removed {Path.GetRelativePath(services.WorkingDirectory, path)}. Review any package, configuration, and source changes separately.");
        await Task.CompletedTask;
        return DotisanExitCode.Success;
    }
}
