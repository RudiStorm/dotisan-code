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
        Assert.Equal(ProjectProfile.Minimal, options.Profile);
        Assert.Equal(JobProvider.None, options.JobProvider);
        Assert.False(options.JobsEnabled);
    }

    [Theory]
    [InlineData(ProjectProfile.Minimal, false, false, false, JobProvider.None)]
    [InlineData(ProjectProfile.Identity, true, false, false, JobProvider.Wolverine)]
    [InlineData(ProjectProfile.Saas, true, true, false, JobProvider.Wolverine)]
    [InlineData(ProjectProfile.Maximal, true, true, true, JobProvider.Wolverine)]
    public void Profiles_expand_to_explicit_dependency_choices(
        ProjectProfile profile,
        bool authentication,
        bool tenancy,
        bool integrations,
        JobProvider provider)
    {
        var options = ProjectOptions.Quick("TodoApp", "C:\\work\\TodoApp").WithProfile(profile);

        Assert.Equal(profile, options.Profile);
        Assert.Equal(authentication, options.AuthenticationEnabled);
        Assert.Equal(tenancy, options.MultiTenancyEnabled);
        Assert.Equal(integrations, options.NotificationsEnabled);
        Assert.Equal(integrations, options.StorageEnabled);
        Assert.Equal(integrations, options.CachingEnabled);
        Assert.Equal(integrations, options.ImportsExportsEnabled);
        Assert.Equal(integrations, options.WebhooksEnabled);
        Assert.Equal(provider, options.JobProvider);
        Assert.Equal(provider == JobProvider.Wolverine, options.JobsEnabled);
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
