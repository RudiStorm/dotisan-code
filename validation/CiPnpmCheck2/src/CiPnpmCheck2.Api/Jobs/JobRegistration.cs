using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Sqlite;
using Wolverine.EntityFrameworkCore;

namespace CiPnpmCheck2.Api.Jobs;

public static class JobRegistration
{
    // DOTISAN:SCHEDULE sample|SampleJob|300|true
    public static void Configure(WolverineOptions options, string connectionString, IConfiguration configuration)
    {
        var jobsEnabled = configuration.GetValue("Dotisan:Jobs:Enabled", true);
        if (jobsEnabled)
        {
            options.PersistMessagesWithSqlite(connectionString);
            options.Policies.UseDurableLocalQueues();
            options.Services.AddHostedService<SampleJobScheduleStarter>();
        }
        options.UseEntityFrameworkCoreTransactions();
        if (jobsEnabled)
        {
            ((IWithFailurePolicies)options.Policies).OnException<Exception>()
                .ScheduleRetry(TimeSpan.FromSeconds(configuration.GetValue("Dotisan:Jobs:RetryDelaySeconds", 5)));
        }
        SampleJobHandler.ConfigureRetry(
            configuration.GetValue("Dotisan:Jobs:MaxAttempts", 3),
            configuration.GetValue("Dotisan:Jobs:RetryDelaySeconds", 5));
    }
}

internal sealed class SampleJobScheduleStarter(IServiceScopeFactory scopeFactory, IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (configuration.GetValue("Dotisan:Jobs:Enabled", true))
        {
            using var scope = scopeFactory.CreateScope();
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.ScheduleAsync(new SampleJob(DateTimeOffset.UtcNow, true), TimeSpan.FromSeconds(300));
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}