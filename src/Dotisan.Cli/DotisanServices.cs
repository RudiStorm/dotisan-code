using System.Diagnostics;
using Dotisan.Core;
using Dotisan.Generators;

namespace Dotisan.Cli;

public sealed record DotisanWorkspace(string Root, string SolutionPath, string ApiProjectPath, string? FrontendDirectory);

public static class DotisanWorkspaceLocator
{
    public static DotisanWorkspace? Find(string startingDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startingDirectory));
        while (directory is not null)
        {
            var solution = directory.EnumerateFiles("*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
            var api = FindProjectFile(directory, "*.Api.csproj");
            if (solution is not null && api is not null)
            {
                var frontend = FindFrontendDirectory(directory);
                return new DotisanWorkspace(directory.FullName, solution.FullName, api.FullName, frontend);
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static FileInfo? FindProjectFile(DirectoryInfo directory, string pattern)
    {
        try
        {
            var searchRoot = directory.GetDirectories("src", SearchOption.TopDirectoryOnly).FirstOrDefault() ?? directory;
            return searchRoot.EnumerateFiles(pattern, SearchOption.AllDirectories)
                .FirstOrDefault(file => !file.FullName.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase) && !file.FullName.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase));
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string? FindFrontendDirectory(DirectoryInfo directory)
    {
        try
        {
            return directory.EnumerateDirectories("src", SearchOption.TopDirectoryOnly)
                .SelectMany(src => src.EnumerateDirectories("*.Web", SearchOption.AllDirectories))
                .FirstOrDefault(web => File.Exists(Path.Combine(web.FullName, "package.json")))
                ?.FullName;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}

public sealed class DefaultDotisanServices : IDotisanServices
{
    private readonly DotisanWorkspace? workspace;
    private readonly string root;

    public DefaultDotisanServices(string? workingDirectory = null)
    {
        root = Path.GetFullPath(workingDirectory ?? Directory.GetCurrentDirectory());
        workspace = DotisanWorkspaceLocator.Find(root);
        PackageManager = ReadPackageManager(workspace?.Root);
    }

    public string WorkingDirectory => workspace?.Root ?? root;
    public string? SolutionPath => workspace?.SolutionPath;
    public string? ApiProjectPath => workspace?.ApiProjectPath;
    public string? FrontendDirectory => workspace?.FrontendDirectory;
    public string PackageManager { get; }

    public async Task<DotisanOperationResult> ScaffoldResourceAsync(string resourceName, CancellationToken cancellationToken)
    {
        if (workspace is null)
            return DotisanOperationResult.Failed("Could not find a generated Dotisan project. Run this command from the project root.");
        var result = await ResourceScaffolder.ScaffoldAsync(workspace.Root, resourceName, cancellationToken);
        return result.Success ? DotisanOperationResult.Succeeded() : DotisanOperationResult.Failed(result.ErrorMessage ?? "Resource scaffolding failed.");
    }

    public async Task<DotisanOperationResult> ScaffoldEndpointAsync(string endpointName, CancellationToken cancellationToken)
    {
        if (workspace is null)
            return DotisanOperationResult.Failed("Could not find a generated Dotisan project. Run this command from the project root.");
        var result = await EndpointScaffolder.ScaffoldAsync(workspace.Root, endpointName, cancellationToken);
        return result.Success ? DotisanOperationResult.Succeeded() : DotisanOperationResult.Failed(result.ErrorMessage ?? "Endpoint scaffolding failed.");
    }

    public async Task<DotisanOperationResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory, IConsole console, CancellationToken cancellationToken)
    {
        try
        {
            await using var process = await StartAsync(fileName, arguments, workingDirectory, console, cancellationToken);
            var exitCode = await process.Completion;
            return exitCode == 0
                ? DotisanOperationResult.Succeeded()
                : DotisanOperationResult.Failed($"'{fileName}' exited with code {exitCode}.", exitCode);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return DotisanOperationResult.Failed($"Could not run '{fileName}': {exception.Message}");
        }
    }

    public Task<IDotisanProcess> StartAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory, IConsole console, CancellationToken cancellationToken) =>
        ProcessDotisanProcess.StartAsync(fileName, arguments, workingDirectory, console, cancellationToken);

    private static string ReadPackageManager(string? projectRoot)
    {
        var packageManager = "pnpm";
        if (projectRoot is not null)
        {
            var configPath = Path.Combine(projectRoot, "dotisan.config");
            if (File.Exists(configPath) && File.ReadAllLines(configPath).Any(line => line.Trim().Equals("package_manager: npm", StringComparison.OrdinalIgnoreCase)))
                packageManager = "npm";
        }

        return OperatingSystem.IsWindows() ? $"{packageManager}.cmd" : packageManager;
    }
}

internal sealed class ProcessDotisanProcess : IDotisanProcess
{
    private readonly Process process;
    private readonly CancellationTokenRegistration cancellationRegistration;

    private ProcessDotisanProcess(Process process, CancellationTokenRegistration cancellationRegistration)
    {
        this.process = process;
        this.cancellationRegistration = cancellationRegistration;
        Completion = WaitForExitAsync();
    }

    public Task<int> Completion { get; }

    public static Task<IDotisanProcess> StartAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory, IConsole console, CancellationToken cancellationToken)
    {
        var executable = fileName;
        IReadOnlyList<string> processArguments = arguments;
        if (OperatingSystem.IsWindows() && fileName.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            executable = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            processArguments = ["/d", "/c", string.Join(" ", new[] { fileName }.Concat(arguments))];
        }

        var startInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in processArguments)
            startInfo.ArgumentList.Add(argument);

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
            throw new InvalidOperationException($"The process '{fileName}' did not start.");
        process.OutputDataReceived += (_, eventArgs) => { if (eventArgs.Data is not null) console.WriteLine(eventArgs.Data); };
        process.ErrorDataReceived += (_, eventArgs) => { if (eventArgs.Data is not null) console.WriteError(eventArgs.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var registration = cancellationToken.Register(() => Kill(process));
        return Task.FromResult<IDotisanProcess>(new ProcessDotisanProcess(process, registration));
    }

    public async ValueTask DisposeAsync()
    {
        cancellationRegistration.Dispose();
        if (!process.HasExited)
            Kill(process);
        await process.WaitForExitAsync();
        process.Dispose();
    }

    private async Task<int> WaitForExitAsync()
    {
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
