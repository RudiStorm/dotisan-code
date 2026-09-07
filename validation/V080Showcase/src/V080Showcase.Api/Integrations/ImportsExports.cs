using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Routing;
using Wolverine;
namespace V080Showcase.Api.Integrations;

public sealed record ImportJob(Guid Id, string Format, string Status, int Processed, int Failed, DateTimeOffset CreatedAtUtc);
public sealed record ImportStatus(Guid Id, string Status, int Processed, int Failed, string? ValidationReport);
public interface IDataExchangeService
{
    Task<ImportJob> StartImportAsync(Stream content, string format, CancellationToken cancellationToken = default);
    Task<ImportStatus?> GetStatusAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Stream> ExportAsync(string format, CancellationToken cancellationToken = default);
}
public sealed record ImportRequested(Guid Id, string Format, string? TenantId, byte[] Content);
public sealed class DataExchangeService(IMessageBus bus) : IDataExchangeService
{
    private readonly ConcurrentDictionary<Guid, ImportStatus> statuses = new();
    public async Task<ImportJob> StartImportAsync(Stream content, string format, CancellationToken cancellationToken = default) { if (format is not ("csv" or "json")) throw new ArgumentException("Only csv and json imports are supported.", nameof(format)); using var memory = new MemoryStream(); await content.CopyToAsync(memory, cancellationToken); var id = Guid.NewGuid(); statuses[id] = new ImportStatus(id, "queued", 0, 0, null); await bus.PublishAsync(new ImportRequested(id, format, null, memory.ToArray())); return new ImportJob(id, format, "queued", 0, 0, DateTimeOffset.UtcNow); }
    public Task<ImportStatus?> GetStatusAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(statuses.TryGetValue(id, out var status) ? status : null);
    public Task<Stream> ExportAsync(string format, CancellationToken cancellationToken = default) => format is "csv" or "json" ? Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(format == "csv" ? "id,name\n" : "[]"))) : throw new ArgumentException("Only csv and json exports are supported.", nameof(format));
}
public static class ImportExportEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints) { var group = endpoints.MapGroup("/api/data"); group.RequireAuthorization(); group.MapPost("/imports", async (IFormFile file, IDataExchangeService service, CancellationToken cancellationToken) => Results.Accepted(value: await service.StartImportAsync(file.OpenReadStream(), Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant(), cancellationToken))); group.MapGet("/imports/{id:guid}", async (Guid id, IDataExchangeService service, CancellationToken cancellationToken) => { var status = await service.GetStatusAsync(id, cancellationToken); return status is null ? Results.NotFound() : Results.Ok(status); }); group.MapGet("/exports/{format}", async (string format, IDataExchangeService service, CancellationToken cancellationToken) => Results.File(await service.ExportAsync(format, cancellationToken), format == "csv" ? "text/csv" : "application/json", $"export.{format}")); }
}