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
        Assert.Contains("dotisan 0.3.0", console.Output);
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

        var exitCode = await app.RunAsync(["doctor"]);

        Assert.Equal(DotisanExitCode.NotImplemented, exitCode);
        Assert.Contains("doctor is not implemented yet", console.ErrorOutput);
    }

    [Fact]
    public async Task Make_resource_delegates_to_workspace_services()
    {
        var console = new MemoryConsole();
        var services = new RecordingServices();
        var app = DotisanApplication.CreateDefault(console, services: services);

        var exitCode = await app.RunAsync(["make:resource", "Customer"]);

        Assert.Equal(DotisanExitCode.Success, exitCode);
        Assert.Equal("Customer", services.ResourceName);
    }

    [Fact]
    public async Task Migrate_runs_standard_ef_command()
    {
        var console = new MemoryConsole();
        var services = new RecordingServices();
        var app = DotisanApplication.CreateDefault(console, services: services);

        var exitCode = await app.RunAsync(["migrate"]);

        Assert.Equal(DotisanExitCode.Success, exitCode);
        Assert.Equal("dotnet", services.FileName);
        Assert.Contains("ef", services.Arguments);
        Assert.Contains("database", services.Arguments);
        Assert.Contains("update", services.Arguments);
    }

    [Fact]
    public async Task Build_can_skip_frontend_and_uses_the_solution()
    {
        var console = new MemoryConsole();
        var services = new RecordingServices();
        var app = DotisanApplication.CreateDefault(console, services: services);

        var exitCode = await app.RunAsync(["build", "--no-frontend"]);

        Assert.Equal(DotisanExitCode.Success, exitCode);
        Assert.Equal("dotnet", services.FileName);
        Assert.Contains("build", services.Arguments);
        Assert.Contains(services.Arguments, argument => argument.EndsWith("App.sln", StringComparison.Ordinal));
    }

    [Fact]
    public async Task New_restores_dotnet_and_installs_frontend_dependencies()
    {
        var console = new MemoryConsole();
        var services = new RecordingServices();
        var app = DotisanApplication.CreateDefault(console, services: services);
        var outputDirectory = Path.Combine(Path.GetTempPath(), "dotisan-new-install-" + Guid.NewGuid().ToString("N"));

        var exitCode = await app.RunAsync(["new", "TodoApp", "--yes", "--package-manager", "npm", "--output", outputDirectory]);

        Assert.Equal(DotisanExitCode.Success, exitCode);
        Assert.Equal(2, services.RunRequests.Count);
        Assert.Equal("dotnet", services.RunRequests[0].FileName);
        Assert.Equal(["restore", Path.Combine(outputDirectory, "TodoApp.sln")], services.RunRequests[0].Arguments);
        Assert.Equal(OperatingSystem.IsWindows() ? "npm.cmd" : "npm", services.RunRequests[1].FileName);
        Assert.Equal(["install"], services.RunRequests[1].Arguments);
        Assert.Equal(Path.Combine(outputDirectory, "src", "TodoApp.Web"), services.RunRequests[1].WorkingDirectory);
    }

    [Fact]
    public async Task New_reports_frontend_install_failure_as_generation_error()
    {
        var console = new MemoryConsole();
        var services = new RecordingServices { FailFrontendInstall = true };
        var app = DotisanApplication.CreateDefault(console, services: services);
        var outputDirectory = Path.Combine(Path.GetTempPath(), "dotisan-new-install-failure-" + Guid.NewGuid().ToString("N"));

        var exitCode = await app.RunAsync(["new", "TodoApp", "--yes", "--package-manager", "npm", "--output", outputDirectory]);

        Assert.Equal(DotisanExitCode.GenerationError, exitCode);
        Assert.Contains("frontend dependency installation failed", console.ErrorOutput);
        Assert.Contains("npm install", console.ErrorOutput);
    }

    [Fact]
    public async Task New_no_restore_skips_dependency_restoration()
    {
        var console = new MemoryConsole();
        var services = new RecordingServices();
        var app = DotisanApplication.CreateDefault(console, services: services);
        var outputDirectory = Path.Combine(Path.GetTempPath(), "dotisan-new-no-restore-" + Guid.NewGuid().ToString("N"));

        var exitCode = await app.RunAsync(["new", "TodoApp", "--yes", "--no-restore", "--output", outputDirectory]);

        Assert.Equal(DotisanExitCode.Success, exitCode);
        Assert.Empty(services.RunRequests);
    }

    [Fact]
    public async Task Dev_lean_starts_the_api_watch_process()
    {
        var console = new MemoryConsole();
        var services = new RecordingServices();
        var app = DotisanApplication.CreateDefault(console, services: services);

        var exitCode = await app.RunAsync(["dev", "--lean", "--environment", "UAT"]);

        Assert.Equal(DotisanExitCode.Success, exitCode);
        Assert.Equal("dotnet", services.StartFileName);
        Assert.Contains("watch", services.StartArguments);
        Assert.Contains("UAT", services.StartArguments);
    }

    [Fact]
    public void Npm_package_manager_uses_a_startable_command_on_windows()
    {
        var root = Path.Combine(Path.GetTempPath(), "dotisan-npm-command-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "src", "App.Api"));
        File.WriteAllText(Path.Combine(root, "App.sln"), string.Empty);
        File.WriteAllText(Path.Combine(root, "src", "App.Api", "App.Api.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(root, "dotisan.config"), "package_manager: npm\n");

        try
        {
            var services = new DefaultDotisanServices(root);

            Assert.Equal(OperatingSystem.IsWindows() ? "npm.cmd" : "npm", services.PackageManager);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Windows_batch_package_manager_processes_can_start_with_arguments()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), "dotisan-npm-process-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "src", "App.Api"));
        File.WriteAllText(Path.Combine(root, "App.sln"), string.Empty);
        File.WriteAllText(Path.Combine(root, "src", "App.Api", "App.Api.csproj"), "<Project />");

        try
        {
            var services = new DefaultDotisanServices(root);
            await using var process = await services.StartAsync(
                "npm.cmd",
                ["--version"],
                root,
                new MemoryConsole(),
                CancellationToken.None);

            Assert.Equal(0, await process.Completion);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private sealed class RecordingServices : IDotisanServices
    {
        public string WorkingDirectory => "C:\\work";
        public string? SolutionPath => "C:\\work\\App.sln";
        public string? ApiProjectPath => "C:\\work\\src\\App.Api\\App.Api.csproj";
        public string? FrontendDirectory => "C:\\work\\src\\App.Web";
        public string? ResourceName { get; private set; }
        public string? FileName { get; private set; }
        public IReadOnlyList<string> Arguments { get; private set; } = [];
        public List<(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory)> RunRequests { get; } = [];
        public bool FailFrontendInstall { get; init; }
        public string? StartFileName { get; private set; }
        public IReadOnlyList<string> StartArguments { get; private set; } = [];

        public Task<DotisanOperationResult> ScaffoldResourceAsync(string resourceName, CancellationToken cancellationToken)
        {
            ResourceName = resourceName;
            return Task.FromResult(DotisanOperationResult.Succeeded());
        }

        public Task<DotisanOperationResult> ScaffoldEndpointAsync(string endpointName, CancellationToken cancellationToken) =>
            Task.FromResult(DotisanOperationResult.Succeeded());

        public Task<DotisanOperationResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory, IConsole console, CancellationToken cancellationToken)
        {
            FileName = fileName;
            Arguments = arguments;
            RunRequests.Add((fileName, arguments, workingDirectory));
            if (FailFrontendInstall && arguments.SequenceEqual(["install"], StringComparer.Ordinal))
                return Task.FromResult(DotisanOperationResult.Failed("npm exited with code 1."));
            return Task.FromResult(DotisanOperationResult.Succeeded());
        }

        public Task<IDotisanProcess> StartAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory, IConsole console, CancellationToken cancellationToken)
        {
            StartFileName = fileName;
            StartArguments = arguments;
            return Task.FromResult<IDotisanProcess>(new CompletedProcess());
        }

        private sealed class CompletedProcess : IDotisanProcess
        {
            public Task<int> Completion => Task.FromResult(0);
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
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
