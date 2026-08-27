namespace Dotisan.Core;

public enum DotisanExitCode
{
    Success = 0,
    UsageError = 2,
    NotImplemented = 3,
    GenerationError = 4
}

public interface IConsole
{
    void WriteLine(string message);
    void WriteError(string message);
}

public interface IPrompts
{
    ProjectOptions AskForProject(string name, string outputDirectory);
}

public sealed record CommandContext(
    IConsole Console,
    IPrompts Prompts,
    IProjectGenerator ProjectGenerator,
    IDotisanServices? Services = null);

public sealed record DotisanOperationResult(bool Success, string? ErrorMessage = null, int ExitCode = 0)
{
    public static DotisanOperationResult Succeeded(int exitCode = 0) => new(true, null, exitCode);

    public static DotisanOperationResult Failed(string message, int exitCode = 1) => new(false, message, exitCode);
}

public interface IDotisanServices
{
    string WorkingDirectory { get; }
    DatabaseProvider Database { get; }
    string? SolutionPath { get; }
    string? ApiProjectPath { get; }
    string? FrontendDirectory { get; }

    Task<DotisanOperationResult> ScaffoldResourceAsync(string resourceName, CancellationToken cancellationToken);

    Task<DotisanOperationResult> ScaffoldEndpointAsync(string endpointName, CancellationToken cancellationToken);

    Task<DotisanOperationResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory, IConsole console, CancellationToken cancellationToken);

    Task<IDotisanProcess> StartAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory, IConsole console, CancellationToken cancellationToken);
}

public interface IDotisanProcess : IAsyncDisposable
{
    Task<int> Completion { get; }
    Task StopAsync();
}

public interface IDotisanCommand
{
    string Name { get; }
    string Description { get; }
    Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}

public interface IProjectGenerator
{
    Task<GenerationResult> GenerateAsync(ProjectOptions options, CancellationToken cancellationToken);
}

public sealed record GenerationResult(bool Success, string OutputDirectory, string? ErrorMessage)
{
    public static GenerationResult Succeeded(string outputDirectory) => new(true, outputDirectory, null);

    public static GenerationResult Failed(string message) => new(false, string.Empty, message);
}
