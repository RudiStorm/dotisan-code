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
        Assert.Equal(5, services.RunRequests.Count);
        Assert.Equal("dotnet", services.RunRequests[0].FileName);
        Assert.Equal(["--version"], services.RunRequests[0].Arguments);
        Assert.Equal("dotnet", services.RunRequests[1].FileName);
        Assert.Equal(["ef", "--version"], services.RunRequests[1].Arguments);
        Assert.Equal(OperatingSystem.IsWindows() ? "npm.cmd" : "npm", services.RunRequests[2].FileName);
        Assert.Equal(["--version"], services.RunRequests[2].Arguments);
        Assert.Equal("dotnet", services.RunRequests[3].FileName);
        Assert.Equal(["restore", Path.Combine(outputDirectory, "TodoApp.sln")], services.RunRequests[3].Arguments);
        Assert.Equal(OperatingSystem.IsWindows() ? "npm.cmd" : "npm", services.RunRequests[4].FileName);
        Assert.Equal(["install"], services.RunRequests[4].Arguments);
        Assert.Equal(Path.Combine(outputDirectory, "src", "TodoApp.Web"), services.RunRequests[4].WorkingDirectory);
    }

    [Fact]
    public async Task New_reports_missing_dotnet_ef_with_install_guidance_and_continues_setup()
    {
        var console = new MemoryConsole();
        var services = new RecordingServices { FailPrerequisite = "dotnet-ef" };
        var app = DotisanApplication.CreateDefault(console, services: services);
        var outputDirectory = Path.Combine(Path.GetTempPath(), "dotisan-new-missing-ef-" + Guid.NewGuid().ToString("N"));

        var exitCode = await app.RunAsync(["new", "TodoApp", "--yes", "--package-manager", "npm", "--output", outputDirectory]);

        Assert.Equal(DotisanExitCode.Success, exitCode);
        Assert.Contains("Missing prerequisite: dotnet-ef", console.ErrorOutput);
        Assert.Contains("dotnet tool install --global dotnet-ef", console.ErrorOutput);
        Assert.Contains("dotnet ef --version", console.ErrorOutput);
        Assert.Contains(services.RunRequests, request => request.Arguments.SequenceEqual(["install"]));
    }

    [Fact]
    public async Task New_checks_the_selected_pnpm_package_manager()
    {
        var console = new MemoryConsole();
        var services = new RecordingServices();
        var app = DotisanApplication.CreateDefault(console, services: services);
        var outputDirectory = Path.Combine(Path.GetTempPath(), "dotisan-new-pnpm-check-" + Guid.NewGuid().ToString("N"));

        var exitCode = await app.RunAsync(["new", "TodoApp", "--yes", "--package-manager", "pnpm", "--output", outputDirectory]);

        Assert.Equal(DotisanExitCode.Success, exitCode);
        Assert.Equal(OperatingSystem.IsWindows() ? "pnpm.cmd" : "pnpm", services.RunRequests[2].FileName);
        Assert.Equal(["--version"], services.RunRequests[2].Arguments);
        Assert.Equal(OperatingSystem.IsWindows() ? "pnpm.cmd" : "pnpm", services.RunRequests[4].FileName);
        Assert.Equal(["install"], services.RunRequests[4].Arguments);
    }

    [Fact]
    public async Task New_reports_missing_package_manager_and_skips_frontend_install()
    {
        var console = new MemoryConsole();
        var services = new RecordingServices { FailPrerequisite = "npm" };
        var app = DotisanApplication.CreateDefault(console, services: services);
        var outputDirectory = Path.Combine(Path.GetTempPath(), "dotisan-new-missing-npm-" + Guid.NewGuid().ToString("N"));

        var exitCode = await app.RunAsync(["new", "TodoApp", "--yes", "--package-manager", "npm", "--output", outputDirectory]);

        Assert.Equal(DotisanExitCode.Success, exitCode);
        Assert.Contains("Missing prerequisite: npm", console.ErrorOutput);
        Assert.Contains("https://nodejs.org/", console.ErrorOutput);
        Assert.DoesNotContain(services.RunRequests, request => request.Arguments.SequenceEqual(["install"]));
        Assert.Contains("frontend dependency installation was skipped", console.ErrorOutput);
    }

    [Fact]
    public async Task New_external_database_reports_missing_docker_with_install_guidance()
    {
        var console = new MemoryConsole();
        var services = new RecordingServices { FailPrerequisite = "docker" };
        var app = DotisanApplication.CreateDefault(console, services: services);
        var outputDirectory = Path.Combine(Path.GetTempPath(), "dotisan-new-missing-docker-" + Guid.NewGuid().ToString("N"));

        var exitCode = await app.RunAsync(["new", "TodoApp", "--yes", "--database", "postgresql", "--no-restore", "--output", outputDirectory]);

        Assert.Equal(DotisanExitCode.Success, exitCode);
        Assert.Contains("Missing prerequisite: Docker", console.ErrorOutput);
        Assert.Contains("https://docs.docker.com/get-docker/", console.ErrorOutput);
        Assert.Contains("docker compose version", console.ErrorOutput);
    }

    [Fact]
    public async Task New_external_database_reports_when_the_docker_daemon_is_not_running()
    {
        var console = new MemoryConsole();
        var services = new RecordingServices { FailPrerequisite = "docker-daemon" };
        var app = DotisanApplication.CreateDefault(console, services: services);
        var outputDirectory = Path.Combine(Path.GetTempPath(), "dotisan-new-docker-daemon-" + Guid.NewGuid().ToString("N"));

        var exitCode = await app.RunAsync(["new", "TodoApp", "--yes", "--database", "mysql", "--no-restore", "--output", outputDirectory]);

        Assert.Equal(DotisanExitCode.Success, exitCode);
        Assert.Contains("Missing prerequisite: Docker daemon", console.ErrorOutput);
        Assert.Contains("Start Docker Desktop", console.ErrorOutput);
        Assert.Contains("docker info", console.ErrorOutput);
    }

    [Fact]
    public async Task New_external_database_confirms_docker_readiness_when_checks_pass()
    {
        var console = new MemoryConsole();
        var services = new RecordingServices();
        var app = DotisanApplication.CreateDefault(console, services: services);
        var outputDirectory = Path.Combine(Path.GetTempPath(), "dotisan-new-docker-ready-" + Guid.NewGuid().ToString("N"));

        var exitCode = await app.RunAsync(["new", "TodoApp", "--yes", "--database", "sqlserver", "--no-restore", "--output", outputDirectory]);

        Assert.Equal(DotisanExitCode.Success, exitCode);
        Assert.Contains("Docker daemon", console.Output);
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
        Assert.Equal(3, services.RunRequests.Count);
        Assert.DoesNotContain(services.RunRequests, request => request.Arguments.Contains("restore", StringComparer.Ordinal));
        Assert.DoesNotContain(services.RunRequests, request => request.Arguments.SequenceEqual(["install"]));
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
    public async Task Dev_cancellation_stops_both_development_services_and_exits_cleanly()
    {
        var console = new MemoryConsole();
        var services = new RecordingServices { BlockProcesses = true };
        var app = DotisanApplication.CreateDefault(console, services: services);
        using var cancellation = new CancellationTokenSource();

        var runTask = app.RunAsync(["dev"], cancellation.Token);
        await services.BothProcessesStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        var exitCode = await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(DotisanExitCode.Success, exitCode);
        Assert.Equal(2, services.BlockingProcesses.Count);
        Assert.All(services.BlockingProcesses, process => Assert.True(process.StopRequested));
        Assert.Contains("Stopping development services", console.Output);
    }

    [Fact]
    public async Task Dev_external_database_starts_compose_before_development_services_and_stops_it_on_exit()
    {
        var console = new MemoryConsole();
        var services = new RecordingServices { Database = DatabaseProvider.PostgreSQL, BlockProcesses = true };
        var app = DotisanApplication.CreateDefault(console, services: services);
        using var cancellation = new CancellationTokenSource();

        var runTask = app.RunAsync(["dev"], cancellation.Token);
        await services.BothProcessesStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        var exitCode = await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(DotisanExitCode.Success, exitCode);
        Assert.Equal("docker", services.RunRequests[0].FileName);
        Assert.Equal(["compose", "up", "-d", "--wait", "--wait-timeout", "120", "database"], services.RunRequests[0].Arguments);
        Assert.Contains(services.RunRequests, request => request.FileName == "docker" && request.Arguments.SequenceEqual(["compose", "stop", "database"]));
    }

    [Fact]
    public async Task Dev_cancellation_during_database_start_still_stops_the_database_container()
    {
        var console = new MemoryConsole();
        var services = new RecordingServices { Database = DatabaseProvider.PostgreSQL, BlockDatabaseStart = true };
        var app = DotisanApplication.CreateDefault(console, services: services);
        using var cancellation = new CancellationTokenSource();

        var runTask = app.RunAsync(["dev"], cancellation.Token);
        await services.DatabaseStartRequested.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        var exitCode = await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(DotisanExitCode.Success, exitCode);
        Assert.Contains(services.RunRequests, request => request.FileName == "docker" && request.Arguments.SequenceEqual(["compose", "stop", "database"]));
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
        public DatabaseProvider Database { get; init; } = DatabaseProvider.SQLite;
        public string? SolutionPath => "C:\\work\\App.sln";
        public string? ApiProjectPath => "C:\\work\\src\\App.Api\\App.Api.csproj";
        public string? FrontendDirectory => "C:\\work\\src\\App.Web";
        public string? ResourceName { get; private set; }
        public string? FileName { get; private set; }
        public IReadOnlyList<string> Arguments { get; private set; } = [];
        public List<(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory)> RunRequests { get; } = [];
        public bool FailFrontendInstall { get; init; }
        public string? FailPrerequisite { get; init; }
        public bool BlockProcesses { get; init; }
        public bool BlockDatabaseStart { get; init; }
        public List<BlockingProcess> BlockingProcesses { get; } = [];
        public TaskCompletionSource BothProcessesStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DatabaseStartRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? StartFileName { get; private set; }
        public IReadOnlyList<string> StartArguments { get; private set; } = [];

        public Task<DotisanOperationResult> ScaffoldResourceAsync(string resourceName, CancellationToken cancellationToken)
        {
            ResourceName = resourceName;
            return Task.FromResult(DotisanOperationResult.Succeeded());
        }

        public Task<DotisanOperationResult> ScaffoldEndpointAsync(string endpointName, CancellationToken cancellationToken) =>
            Task.FromResult(DotisanOperationResult.Succeeded());

        public async Task<DotisanOperationResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory, IConsole console, CancellationToken cancellationToken)
        {
            FileName = fileName;
            Arguments = arguments;
            RunRequests.Add((fileName, arguments, workingDirectory));
            if (BlockDatabaseStart && fileName == "docker" && arguments.SequenceEqual(["compose", "up", "-d", "--wait", "--wait-timeout", "120", "database"]))
            {
                DatabaseStartRequested.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return DotisanOperationResult.Failed("Database startup was cancelled.");
                }
            }
            if (FailPrerequisite == "dotnet-ef" && fileName == "dotnet" && arguments.SequenceEqual(["ef", "--version"]))
                return DotisanOperationResult.Failed("dotnet-ef is not installed.");
            if (FailPrerequisite == "npm" && arguments.SequenceEqual(["--version"]))
                return DotisanOperationResult.Failed("npm is not installed.");
            if (FailPrerequisite == "docker" && fileName == "docker" && arguments.SequenceEqual(["compose", "version"]))
                return DotisanOperationResult.Failed("Docker is not installed.");
            if (FailPrerequisite == "docker-daemon" && fileName == "docker" && arguments.SequenceEqual(["info", "--format", "{{.ServerVersion}}"]))
                return DotisanOperationResult.Failed("Docker daemon is not running.");
            if (FailFrontendInstall && arguments.SequenceEqual(["install"], StringComparer.Ordinal))
                return DotisanOperationResult.Failed("npm exited with code 1.");
            return DotisanOperationResult.Succeeded();
        }

        public Task<IDotisanProcess> StartAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory, IConsole console, CancellationToken cancellationToken)
        {
            StartFileName = fileName;
            StartArguments = arguments;
            if (BlockProcesses)
            {
                var process = new BlockingProcess();
                BlockingProcesses.Add(process);
                if (BlockingProcesses.Count == 2)
                    BothProcessesStarted.TrySetResult();
                return Task.FromResult<IDotisanProcess>(process);
            }

            return Task.FromResult<IDotisanProcess>(new CompletedProcess());
        }

        public sealed class BlockingProcess : IDotisanProcess
        {
            private readonly TaskCompletionSource<int> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<int> Completion => completion.Task;
            public bool StopRequested { get; private set; }

            public Task StopAsync()
            {
                StopRequested = true;
                completion.TrySetResult(0);
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                completion.TrySetResult(0);
                return ValueTask.CompletedTask;
            }
        }

        private sealed class CompletedProcess : IDotisanProcess
        {
            public Task<int> Completion => Task.FromResult(0);
            public Task StopAsync() => Task.CompletedTask;
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
