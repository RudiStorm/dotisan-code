using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Configuration;
using Wolverine.Runtime.Handlers;

namespace CiPnpmCheck.Api.Jobs;

public sealed partial class SampleJobHandler : IHandlerConfiguration
{
    private static int _maxAttempts = 3;
    public static int AttemptCount { get; private set; }
    public static int FailuresBeforeSuccess { get; set; }
    public static int ProcessedCount { get; private set; }

    public static void ResetTestCounters()
    {
        AttemptCount = 0;
        ProcessedCount = 0;
        FailuresBeforeSuccess = 0;
    }

    public static void ConfigureRetry(int maxAttempts, int retryDelaySeconds)
    {
        _ = retryDelaySeconds;
        _maxAttempts = Math.Max(1, maxAttempts);
    }

    public static void Configure(HandlerChain chain)
    {
        chain.Failures.MaximumAttempts = _maxAttempts;
    }

    public static OutgoingMessages Handle(SampleJob message, ILogger<SampleJobHandler> logger)
    {
        AttemptCount++;
        if (FailuresBeforeSuccess > 0)
        {
            FailuresBeforeSuccess--;
            throw new InvalidOperationException("Configured sample-job test failure.");
        }
        ProcessedCount++;
        LogProcessed(logger, message.EnqueuedAt);
        var messages = new OutgoingMessages();
        if (message.Recurring)
            messages.Delay(new SampleJob(DateTimeOffset.UtcNow, true), TimeSpan.FromSeconds(300));
        return messages;
    }

    [LoggerMessage(EventId = 6000, Level = LogLevel.Information, Message = "Processed sample job enqueued at {EnqueuedAt}.")]
    private static partial void LogProcessed(ILogger logger, DateTimeOffset enqueuedAt);
}