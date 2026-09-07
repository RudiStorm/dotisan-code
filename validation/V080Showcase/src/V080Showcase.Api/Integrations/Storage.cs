using Microsoft.AspNetCore.Routing;
namespace V080Showcase.Api.Integrations;

public sealed record StoredFile(string Key, string ContentType, long Length, DateTimeOffset CreatedAtUtc);
public interface IFileStorage
{
    Task<StoredFile> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
public sealed class LocalFileStorage(IHostEnvironment environment, IConfiguration configuration) : IFileStorage
{
    private string Resolve(string key) { var root = Path.GetFullPath(configuration["Storage:LocalRoot"] ?? Path.Combine(environment.ContentRootPath, "storage")); var path = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar))); if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid storage key."); return path; }
    public async Task<StoredFile> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default) { if (content.Length > configuration.GetValue<long>("Storage:MaxBytes", 10_485_760)) throw new InvalidDataException("File exceeds configured size limit."); var path = Resolve(key); Directory.CreateDirectory(Path.GetDirectoryName(path)!); await using var target = File.Create(path); await content.CopyToAsync(target, cancellationToken); return new StoredFile(key, contentType, content.Length, DateTimeOffset.UtcNow); }
    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default) { var path = Resolve(key); return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null); }
    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) { var path = Resolve(key); if (File.Exists(path)) File.Delete(path); return Task.CompletedTask; }
}
public sealed class S3CompatibleFileStorage : IFileStorage
{
    public Task<StoredFile> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default) => throw new NotSupportedException("Implement the S3-compatible adapter using Storage:S3 configuration.");
    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException("Implement the S3-compatible adapter using Storage:S3 configuration.");
    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException("Implement the S3-compatible adapter using Storage:S3 configuration.");
}
public static class StorageEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints) { var group = endpoints.MapGroup("/api/files"); group.RequireAuthorization(); group.MapPost("", async (IFormFile file, IFileStorage storage, CancellationToken cancellationToken) => { if (file.Length == 0 || string.IsNullOrWhiteSpace(file.ContentType)) return Results.BadRequest(new { code = "invalid_file" }); var key = $"uploads/{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}"; await using var stream = file.OpenReadStream(); return Results.Ok(await storage.PutAsync(key, stream, file.ContentType, cancellationToken)); }); group.MapGet("/{**key}", async (string key, IFileStorage storage, CancellationToken cancellationToken) => { var stream = await storage.OpenReadAsync(key, cancellationToken); return stream is null ? Results.NotFound() : Results.File(stream, "application/octet-stream"); }); group.MapDelete("/{**key}", async (string key, IFileStorage storage, CancellationToken cancellationToken) => { await storage.DeleteAsync(key, cancellationToken); return Results.NoContent(); }); }
}