using Dotisan.Core;

namespace Dotisan.Core.Tests;

public sealed class ProjectOptionsTests
{
    [Fact]
    public void Quick_defaults_use_sqlite_without_auth_or_tenancy()
    {
        var options = ProjectOptions.Quick("TodoApp", "C:\\work\\TodoApp");

        Assert.Equal("TodoApp", options.Name);
        Assert.Equal("C:\\work\\TodoApp", options.OutputDirectory);
        Assert.Equal(DatabaseProvider.SQLite, options.Database);
        Assert.False(options.AuthenticationEnabled);
        Assert.Equal(RegistrationPolicy.Disabled, options.Registration);
        Assert.False(options.MultiTenancyEnabled);
        Assert.Equal(PackageManager.Pnpm, options.PackageManager);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not valid")]
    [InlineData("1StartsWithNumber")]
    public void Invalid_project_names_are_rejected(string name)
    {
        Assert.Throws<ArgumentException>(() => ProjectOptions.Quick(name, "C:\\work"));
    }
}
