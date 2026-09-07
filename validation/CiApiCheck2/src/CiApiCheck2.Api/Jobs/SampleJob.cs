namespace CiApiCheck2.Api.Jobs;

public sealed record SampleJob(DateTimeOffset EnqueuedAt, bool Recurring = false);