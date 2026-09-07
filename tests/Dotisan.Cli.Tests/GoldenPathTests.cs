using Dotisan.Cli;
using Dotisan.Core;
using Dotisan.Testing;

namespace Dotisan.Cli.Tests;

public sealed class GoldenPathTests
{
    [Fact]
    public async Task Resource_generation_updates_contract_and_preserves_a_buildable_frontend()
    {
        using var directory = new TemporaryDirectory();
        var root = Path.Combine(directory.Path, "GoldenApp");

        var creationConsole = new MemoryConsole();
        var creation = DotisanApplication.CreateDefault(creationConsole);
        Assert.Equal(
            DotisanExitCode.Success,
            await creation.RunAsync(["new", "GoldenApp", "--yes", "--auth", "yes", "--registration", "public", "--no-restore", "--output", root]));

        var console = new MemoryConsole();
        var services = new DefaultDotisanServices(root);
        Assert.True((await services.RunAsync("dotnet", ["restore", Path.Combine(root, "GoldenApp.sln")], root, console, CancellationToken.None)).Success, console.ErrorOutput);
        Assert.True((await services.RunAsync("dotnet", ["build", Path.Combine(root, "GoldenApp.sln"), "--no-restore"], root, console, CancellationToken.None)).Success, console.ErrorOutput);
        var app = DotisanApplication.CreateDefault(console, services);
        Assert.Equal(DotisanExitCode.Success, await app.RunAsync(["make:resource", "Customer"]));
        Assert.Equal(DotisanExitCode.Success, await app.RunAsync(["generate"]));
        Assert.Equal(DotisanExitCode.Success, await app.RunAsync(["generate", "--check"]));

        var generatedServices = await File.ReadAllTextAsync(
            Path.Combine(root, "src", "GoldenApp.Web", "src", "dotisan", "services.ts"));
        Assert.Contains("listCustomers", generatedServices, StringComparison.Ordinal);
        Assert.Contains("customerId", generatedServices, StringComparison.Ordinal);
    }

    private sealed class MemoryConsole : IConsole
    {
        public string ErrorOutput { get; private set; } = string.Empty;

        public void WriteLine(string message) { }
        public void WriteError(string message) => ErrorOutput += message + Environment.NewLine;
    }
}
