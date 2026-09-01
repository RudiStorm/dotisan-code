namespace Dotisan.Core.Jobs;

public sealed class JobOptions
{
    public bool Enabled { get; set; } = true;

    public int MaxAttempts { get; set; } = 3;

    public int RetryDelaySeconds { get; set; } = 5;

    public JobOptions Validate()
    {
        if (MaxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(MaxAttempts), "MaxAttempts must be at least 1.");
        if (RetryDelaySeconds < 1)
            throw new ArgumentOutOfRangeException(nameof(RetryDelaySeconds), "RetryDelaySeconds must be at least 1.");
        return this;
    }
}
