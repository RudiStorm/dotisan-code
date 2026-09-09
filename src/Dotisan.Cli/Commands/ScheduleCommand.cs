using Dotisan.Core;
using Dotisan.Core.Jobs;

namespace Dotisan.Cli;

internal sealed class ScheduleCommand : WorkspaceCommand
{
    public override string Name => "schedule";
    public override string Description => "Inspect generated recurring schedules.";

    public override Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (!arguments.SequenceEqual(["list"], StringComparer.OrdinalIgnoreCase)) return Task.FromResult(Fail(context.Console, "Usage: dotisan schedule list.", DotisanExitCode.UsageError));
        if (!TryGetServices(context, out var services) || services.ApiProjectPath is null) return Task.FromResult(Fail(context.Console, "Could not find a generated Dotisan project. Run this command from the project root."));
        var jobsFile = Path.Combine(Path.GetDirectoryName(services.ApiProjectPath)!, "Jobs", "JobRegistration.cs");
        if (!File.Exists(jobsFile)) return Task.FromResult(Fail(context.Console, "This project does not contain generated scheduling support. Regenerate it with a current Dotisan CLI."));
        var schedules = JobScheduleReader.Read(jobsFile);
        if (schedules.Count == 0) context.Console.WriteLine("No recurring schedules are declared.");
        else foreach (var schedule in schedules) context.Console.WriteLine($"{schedule.Name}: {schedule.MessageType} every {schedule.Interval} ({(schedule.Enabled ? "enabled" : "disabled")})");
        context.Console.WriteLine($"Edit: {Path.GetRelativePath(services.WorkingDirectory, jobsFile)}");
        return Task.FromResult(DotisanExitCode.Success);
    }
}
