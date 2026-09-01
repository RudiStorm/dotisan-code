using Dotisan.Core.Jobs;

namespace Dotisan.Core.Tests;

public sealed class JobOptionsTests
{
    [Fact]
    public void Defaults_are_safe_for_local_development()
    {
        var options = new JobOptions();

        Assert.True(options.Enabled);
        Assert.Equal(3, options.MaxAttempts);
        Assert.Equal(5, options.RetryDelaySeconds);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(3, 0)]
    public void Invalid_retry_values_are_rejected(int maxAttempts, int retryDelaySeconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JobOptions
        {
            MaxAttempts = maxAttempts,
            RetryDelaySeconds = retryDelaySeconds
        }.Validate());
    }
}
