using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using V080ProductionCheck.Api.Data;
namespace V080ProductionCheck.Api.Integrations;

public sealed class WebhookDelivery { public Guid Id { get; set; } public string EventType { get; set; } = string.Empty; public string Endpoint { get; set; } = string.Empty; public string Signature { get; set; } = string.Empty; public string PayloadJson { get; set; } = "{}"; public string Status { get; set; } = "queued"; public int Attempts { get; set; } public int RetryCount { get; set; } public DateTimeOffset CreatedAtUtc { get; set; } }
public sealed record WebhookSubscription(Guid Id, string EventType, Uri Endpoint, bool Enabled);
public interface IWebhookDispatcher
{
    Task DispatchAsync(string eventType, object payload, CancellationToken cancellationToken = default);
    Task<bool> ReplayAsync(Guid deliveryId, CancellationToken cancellationToken = default);
}
public sealed class HmacWebhookDispatcher(AppDbContext db, IHttpClientFactory clients, IConfiguration configuration) : IWebhookDispatcher
{
    public async Task DispatchAsync(string eventType, object payload, CancellationToken cancellationToken = default) { var body = JsonSerializer.Serialize(payload); var secret = configuration["Webhooks:SigningSecret"] ?? throw new InvalidOperationException("Webhooks:SigningSecret must be configured."); var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))); var maxAttempts = Math.Clamp(configuration.GetValue("Webhooks:MaxAttempts", 3), 1, 10); foreach (var endpoint in configuration.GetSection("Webhooks:Endpoints").Get<string[]>() ?? []) { var delivery = new WebhookDelivery { Id = Guid.NewGuid(), EventType = eventType, Endpoint = endpoint, Signature = signature, PayloadJson = body, CreatedAtUtc = DateTimeOffset.UtcNow }; db.WebhookDeliveries.Add(delivery); for (var attempt = 1; attempt <= maxAttempts; attempt++) { delivery.Attempts = attempt; try { using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new StringContent(body, Encoding.UTF8, "application/json") }; request.Headers.Add("X-Dotisan-Signature", signature); using var response = await clients.CreateClient().SendAsync(request, cancellationToken); if (response.IsSuccessStatusCode) { delivery.Status = "delivered"; break; } delivery.Status = "failed"; } catch { delivery.Status = "failed"; } if (attempt < maxAttempts) { delivery.RetryCount++; await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), cancellationToken); } } await db.SaveChangesAsync(cancellationToken); } }
    public async Task<bool> ReplayAsync(Guid deliveryId, CancellationToken cancellationToken = default) { var delivery = await db.WebhookDeliveries.SingleOrDefaultAsync(item => item.Id == deliveryId, cancellationToken); if (delivery is null) return false; using var request = new HttpRequestMessage(HttpMethod.Post, delivery.Endpoint) { Content = new StringContent(delivery.PayloadJson, Encoding.UTF8, "application/json") }; request.Headers.Add("X-Dotisan-Signature", delivery.Signature); using var response = await clients.CreateClient().SendAsync(request, cancellationToken); delivery.Attempts++; delivery.RetryCount++; delivery.Status = response.IsSuccessStatusCode ? "delivered" : "failed"; await db.SaveChangesAsync(cancellationToken); return response.IsSuccessStatusCode; }
}
public static class WebhookEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints) { var group = endpoints.MapGroup("/api/webhooks"); group.RequireAuthorization(); group.MapGet("/deliveries", async (AppDbContext db, CancellationToken cancellationToken) => Results.Ok(await db.WebhookDeliveries.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync(cancellationToken))); group.MapPost("/deliveries/{id:guid}/replay", async (Guid id, IWebhookDispatcher dispatcher, CancellationToken cancellationToken) => await dispatcher.ReplayAsync(id, cancellationToken) ? Results.Accepted($"/api/webhooks/deliveries/{id}", new { deliveryId = id, status = "delivered" }) : Results.NotFound()); }
}