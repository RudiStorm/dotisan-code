using Dotisan.Core;

namespace Dotisan.Cli;

public sealed class DotisanCommandRegistry
{
    private readonly Dictionary<string, IDotisanCommand> commands = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<IDotisanCommand> Commands => commands.Values.OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase);

    public void Register(IDotisanCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        commands[command.Name] = command;
    }

    public bool TryGet(string name, out IDotisanCommand? command) => commands.TryGetValue(name, out command);
}
