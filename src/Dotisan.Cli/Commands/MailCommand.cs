using Dotisan.Core;

namespace Dotisan.Cli;

internal sealed class MailCommand : WorkspaceCommand
{
    public override string Name => "mail";
    public override string Description => "Show or open the local Mailpit inbox.";

    public override Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Any(argument => argument is not "--open")) return Task.FromResult(Fail(context.Console, "Usage: dotisan mail [--open].", DotisanExitCode.UsageError));
        if (!TryGetServices(context, out var services) || services.MailProvider != MailProvider.Mailpit) return Task.FromResult(Fail(context.Console, "Mailpit is not selected. Generate the project with --mail-provider mailpit."));
        const string url = "http://localhost:8025";
        if (!arguments.Contains("--open", StringComparer.OrdinalIgnoreCase)) { context.Console.WriteLine($"Mailpit inbox: {url}"); return Task.FromResult(DotisanExitCode.Success); }
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true }); context.Console.WriteLine($"Opened Mailpit inbox: {url}"); return Task.FromResult(DotisanExitCode.Success); }
        catch (InvalidOperationException exception) { return Task.FromResult(Fail(context.Console, $"Could not open Mailpit inbox: {exception.Message}")); }
    }
}
