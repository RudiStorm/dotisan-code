namespace V080ShowcaseVerified.Api.Jobs;

public sealed record SampleJob(DateTimeOffset EnqueuedAt, bool Recurring = false);