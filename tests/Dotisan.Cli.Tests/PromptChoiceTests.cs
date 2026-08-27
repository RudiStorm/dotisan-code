using Dotisan.Cli;
using Dotisan.Core;

namespace Dotisan.Cli.Tests;

public sealed class PromptChoiceTests
{
    [Fact]
    public void Quick_prompt_captures_the_specified_choices()
    {
        var answers = new Queue<string?>(["", "PostgreSQL", "yes", "Invite only", "yes", "npm"]);
        var prompts = new DefaultPrompts(
            new CapturingConsole(),
            isInputRedirected: () => false,
            readLine: () => answers.Dequeue());

        var options = prompts.AskForProject("TodoApp", "C:\\work\\TodoApp");

        Assert.Equal(DatabaseProvider.PostgreSQL, options.Database);
        Assert.True(options.AuthenticationEnabled);
        Assert.Equal(RegistrationPolicy.InviteOnly, options.Registration);
        Assert.True(options.MultiTenancyEnabled);
        Assert.Equal(PackageManager.Npm, options.PackageManager);
    }

    [Fact]
    public void Quick_prompt_accepts_numbers_for_each_menu()
    {
        var answers = new Queue<string?>(["1", "3", "2", "2", "2", "1"]);
        var prompts = new DefaultPrompts(
            new CapturingConsole(),
            isInputRedirected: () => false,
            readLine: () => answers.Dequeue());

        var options = prompts.AskForProject("TodoApp", "C:\\work\\TodoApp");

        Assert.Equal(DatabaseProvider.PostgreSQL, options.Database);
        Assert.True(options.AuthenticationEnabled);
        Assert.Equal(RegistrationPolicy.InviteOnly, options.Registration);
        Assert.True(options.MultiTenancyEnabled);
        Assert.Equal(PackageManager.Pnpm, options.PackageManager);
    }

    [Fact]
    public void Quick_prompt_displays_numbered_choices()
    {
        var console = new CapturingConsole();
        var answers = new Queue<string?>(["", "", "", "", ""]);
        var prompts = new DefaultPrompts(
            console,
            isInputRedirected: () => false,
            readLine: () => answers.Dequeue());

        prompts.AskForProject("TodoApp", "C:\\work\\TodoApp");

        Assert.Contains("1) Quick", console.Output);
        Assert.Contains("2) Advanced", console.Output);
        Assert.Contains("1) SQLite", console.Output);
        Assert.Contains("2) SQL Server", console.Output);
        Assert.Contains("1) No", console.Output);
        Assert.Contains("2) Yes", console.Output);
    }

    private sealed class CapturingConsole : IConsole
    {
        public string Output { get; private set; } = string.Empty;

        public void WriteLine(string message) => Output += message + Environment.NewLine;
        public void WriteError(string message) { }
    }
}
