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

public sealed record CommandContext(IConsole Console, IPrompts Prompts, IProjectGenerator ProjectGenerator);

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
