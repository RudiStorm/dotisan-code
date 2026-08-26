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

    private sealed class CapturingConsole : IConsole
    {
        public void WriteLine(string message) { }
        public void WriteError(string message) { }
    }
}
