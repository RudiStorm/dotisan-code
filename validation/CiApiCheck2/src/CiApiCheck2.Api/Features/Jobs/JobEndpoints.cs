using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;
using CiApiCheck2.Api.Jobs;

namespace CiApiCheck2.Api.Features.Jobs;

public static class JobEndpoints
{
    public static void MapJobEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/jobs/sample", async (IMessageBus bus, CancellationToken cancellationToken) =>
        {
            await bus.SendAsync(new SampleJob(DateTimeOffset.UtcNow));
            return Results.Accepted();
        }).WithName("EnqueueSampleJob").WithTags("Jobs");

        endpoints.MapPost("/api/jobs/sample/schedule", async (IMessageBus bus, CancellationToken cancellationToken) =>
        {
            await bus.ScheduleAsync(new SampleJob(DateTimeOffset.UtcNow), TimeSpan.FromMinutes(5));
            return Results.Accepted();
        }).WithName("ScheduleSampleJob").WithTags("Jobs");
    }
}