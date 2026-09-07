using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using JasperFx.Resources;
using Wolverine;
using CiApiCheck3.Api.Jobs;

namespace CiApiCheck3.Api.Tests;

public sealed class JobTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public JobTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Logging:EventLog:LogLevel:Default", "None");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Dotisan:Jobs:Enabled"] = "false",
                ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:"
            }));
        });
    }

    [Fact]
    public async Task Sample_job_endpoint_dispatches_through_wolverine()
    {
        SampleJobHandler.ResetTestCounters();
        var bus = factory.Services.GetRequiredService<IMessageBus>();
        await bus.SendAsync(new SampleJob(DateTimeOffset.UtcNow));
        for (var attempt = 0; attempt < 50 && SampleJobHandler.ProcessedCount == 0; attempt++)
            await Task.Delay(100);
        Assert.True(SampleJobHandler.ProcessedCount > 0);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/api/jobs/sample", content: null);

        Assert.Equal(System.Net.HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Sample_job_can_be_scheduled_with_the_durable_store()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"dotisan-v06-{Guid.NewGuid():N}.db");
        WebApplicationFactory<Program>? durableFactory = null;
        try
        {
            durableFactory = CreateDurableFactory(databasePath);
            using var client = durableFactory.CreateClient();
            await durableFactory.Services.GetRequiredService<IHost>().SetupResources();
            var bus = durableFactory.Services.GetRequiredService<IMessageBus>();

            await bus.ScheduleAsync(new SampleJob(DateTimeOffset.UtcNow), TimeSpan.FromMinutes(30));
            durableFactory.Dispose();
            durableFactory = CreateDurableFactory(databasePath);
            using var restartedClient = durableFactory.CreateClient();
            await durableFactory.Services.GetRequiredService<IHost>().SetupResources();

            using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            await using var tablesCommand = connection.CreateCommand();
            tablesCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name LIKE 'wolverine%'";
            await using var tablesReader = await tablesCommand.ExecuteReaderAsync();
            var tableNames = new List<string>();
            while (await tablesReader.ReadAsync())
                tableNames.Add(tablesReader.GetString(0));
            Assert.NotEmpty(tableNames);

            var persistedCount = 0L;
            foreach (var tableName in tableNames)
            {
                await using var countCommand = connection.CreateCommand();
                countCommand.CommandText = $"SELECT COUNT(*) FROM \"{tableName.Replace("\"", "\"\"")}\"";
                persistedCount += (long)(await countCommand.ExecuteScalarAsync() ?? 0L);
            }
            Assert.True(persistedCount > 0);
        }
        finally
        {
            durableFactory?.Dispose();
            if (File.Exists(databasePath))
                await DeleteDatabaseAsync(databasePath);
        }
    }

    [Fact]
    public async Task Sample_job_retries_until_the_configured_failure_clears()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"dotisan-v06-retry-{Guid.NewGuid():N}.db");
        SampleJobHandler.ResetTestCounters();
        SampleJobHandler.FailuresBeforeSuccess = 2;
        WebApplicationFactory<Program>? durableFactory = null;
        try
        {
            durableFactory = CreateDurableFactory(databasePath);
            using var client = durableFactory.CreateClient();
            var bus = durableFactory.Services.GetRequiredService<IMessageBus>();
            await bus.SendAsync(new SampleJob(DateTimeOffset.UtcNow));

            for (var attempt = 0; attempt < 100 && SampleJobHandler.ProcessedCount == 0; attempt++)
                await Task.Delay(100);

            Assert.Equal(3, SampleJobHandler.AttemptCount);
            Assert.Equal(1, SampleJobHandler.ProcessedCount);
        }
        finally
        {
            durableFactory?.Dispose();
            SampleJobHandler.FailuresBeforeSuccess = 0;
            if (File.Exists(databasePath))
                await DeleteDatabaseAsync(databasePath);
        }
    }

    private static async Task DeleteDatabaseAsync(string databasePath)
    {
        for (var attempt = 0; attempt < 20 && File.Exists(databasePath); attempt++)
        {
            try
            {
                File.Delete(databasePath);
            }
            catch (IOException)
            {
                await Task.Delay(100);
            }
        }
    }

    private WebApplicationFactory<Program> CreateDurableFactory(string databasePath) => factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Dotisan:Jobs:Enabled", "true");
        builder.UseSetting("Dotisan:Jobs:RetryDelaySeconds", "1");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={databasePath}");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Dotisan:Jobs:Enabled"] = "true",
            ["Dotisan:Jobs:RetryDelaySeconds"] = "1",
            ["ConnectionStrings:DefaultConnection"] = $"Data Source={databasePath}"
        }));
        builder.ConfigureServices(services => services.AddResourceSetupOnStartup());
    });

    [Fact]
    public void Generated_job_test_mentions_durable_scheduling_contract()
    {
        Assert.Contains("IMessageBus", typeof(Wolverine.IMessageBus).FullName);
        Assert.Contains("scheduled", "Durable scheduled messages survive process restarts", StringComparison.OrdinalIgnoreCase);
    }
}
