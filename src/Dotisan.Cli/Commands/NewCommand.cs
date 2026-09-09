using Dotisan.Core;

namespace Dotisan.Cli;

internal sealed class NewCommand : IDotisanCommand
{
    private readonly LegacyNewCommand implementation = new();

    public string Name => implementation.Name;
    public string Description => implementation.Description;

    public Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken) => implementation.ExecuteAsync(context, arguments, cancellationToken);
}
