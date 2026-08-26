using Dotisan.Cli;
using Dotisan.Core;

namespace Dotisan.Cli.Tests;

public sealed class CliApplicationTests
{
    [Fact]
    public async Task Version_flag_prints_version_and_succeeds()
    {
        var console = new MemoryConsole();
        var app = DotisanApplication.CreateDefault(console);

        var exitCode = await app.RunAsync(["--version"]);

        Assert.Equal(DotisanExitCode.Success, exitCode);
        Assert.Contains("dotisan 0.1.0", console.Output);
    }

    [Fact]
    public async Task Help_lists_the_first_slice_commands()
    {
        var console = new MemoryConsole();
        var app = DotisanApplication.CreateDefault(console);

        var exitCode = await app.RunAsync(["help"]);

        Assert.Equal(DotisanExitCode.Success, exitCode);
        Assert.Contains("dotisan new <ProjectName>", console.Output);
        Assert.Contains("make:resource", console.Output);
    }

    [Fact]
    public async Task Registered_command_is_dispatched_without_knowing_its_implementation()
    {
        var console = new MemoryConsole();
        var registry = new DotisanCommandRegistry();
        var command = new RecordingCommand();
        registry.Register(command);
        var app = new DotisanApplication(registry, console, new DefaultPrompts(), new NoOpProjectGenerator());

        var exitCode = await app.RunAsync(["custom", "argument"]);

        Assert.Equal(DotisanExitCode.Success, exitCode);
        Assert.Equal(["argument"], command.Arguments);
    }

    [Fact]
    public async Task Future_command_returns_helpful_not_implemented_error()
    {
        var console = new MemoryConsole();
        var app = DotisanApplication.CreateDefault(console);

        var exitCode = await app.RunAsync(["migrate"]);

        Assert.Equal(DotisanExitCode.NotImplemented, exitCode);
        Assert.Contains("migrate is not implemented yet", console.ErrorOutput);
        Assert.Contains("dotnet ef", console.ErrorOutput);
    }

    private sealed class RecordingCommand : IDotisanCommand
    {
        public string Name => "custom";
        public string Description => "A test command.";
        public IReadOnlyList<string> Arguments { get; private set; } = [];

        public Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            Arguments = arguments;
            return Task.FromResult(DotisanExitCode.Success);
        }
    }

    private sealed class MemoryConsole : IConsole
    {
        public string Output { get; private set; } = string.Empty;
        public string ErrorOutput { get; private set; } = string.Empty;

        public void WriteLine(string message) => Output += message + Environment.NewLine;
        public void WriteError(string message) => ErrorOutput += message + Environment.NewLine;
    }

    private sealed class NoOpProjectGenerator : IProjectGenerator
    {
        public Task<GenerationResult> GenerateAsync(ProjectOptions options, CancellationToken cancellationToken) =>
            Task.FromResult(GenerationResult.Succeeded(options.OutputDirectory));
    }
}
