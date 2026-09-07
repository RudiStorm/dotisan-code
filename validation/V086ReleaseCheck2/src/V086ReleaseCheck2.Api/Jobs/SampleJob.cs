namespace V086ReleaseCheck2.Api.Jobs;

public sealed record SampleJob(DateTimeOffset EnqueuedAt, bool Recurring = false);