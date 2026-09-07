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

        var app = DotisanApplication.CreateDefault(new MemoryConsole(), new DefaultDotisanServices(root));
        Assert.Equal(DotisanExitCode.Success, await app.RunAsync(["make:resource", "Customer"]));
        Assert.Equal(DotisanExitCode.Success, await app.RunAsync(["generate"]));
        Assert.Equal(DotisanExitCode.Success, await app.RunAsync(["generate", "--check"]));

        var services = await File.ReadAllTextAsync(
            Path.Combine(root, "src", "GoldenApp.Web", "src", "dotisan", "services.ts"));
        Assert.Contains("listCustomers", services, StringComparison.Ordinal);
        Assert.Contains("customerId", services, StringComparison.Ordinal);
    }

    private sealed class MemoryConsole : IConsole
    {
        public void WriteLine(string message) { }
        public void WriteError(string message) { }
    }
}
