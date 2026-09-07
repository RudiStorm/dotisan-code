using Microsoft.AspNetCore.DataProtection;
using System.Net.Http.Headers;
using System.Text.Json;

namespace V086Production.Api.Integrations;

// Optional examples. Register only after supplying secrets through deployment configuration.
public sealed class SendGridEmailProvider(IHttpClientFactory clients, IConfiguration configuration) : IEmailProvider
{
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration["Mail:SendGrid:ApiKey"] ?? throw new InvalidOperationException("Mail:SendGrid:ApiKey is required."));
        request.Content = JsonContent.Create(new { personalizations = new[] { new { to = new[] { new { email = recipient } } } }, from = new { email = configuration["Mail:SendGrid:From"] ?? throw new InvalidOperationException("Mail:SendGrid:From is required.") }, subject, content = new[] { new { type = "text/plain", value = body } } });
        (await clients.CreateClient().SendAsync(request, cancellationToken)).EnsureSuccessStatusCode();
    }
}

public sealed class MailgunEmailProvider(IHttpClientFactory clients, IConfiguration configuration) : IEmailProvider
{
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        var domain = configuration["Mail:Mailgun:Domain"] ?? throw new InvalidOperationException("Mail:Mailgun:Domain is required.");
        var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.mailgun.net/v3/{domain}/messages") { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["from"] = configuration["Mail:Mailgun:From"] ?? throw new InvalidOperationException("Mail:Mailgun:From is required."), ["to"] = recipient, ["subject"] = subject, ["text"] = body }) };
        var key = configuration["Mail:Mailgun:ApiKey"] ?? throw new InvalidOperationException("Mail:Mailgun:ApiKey is required.");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"api:{key}")));
        (await clients.CreateClient().SendAsync(request, cancellationToken)).EnsureSuccessStatusCode();
    }
}

public sealed class ConfiguredOAuth2Provider(IHttpClientFactory clients, IConfiguration configuration, IExternalLoginStateStore stateStore) : IExternalLoginProvider
{
    public string Name => configuration["OAuth:Name"] ?? "oauth2";
    public string DisplayName => configuration["OAuth:DisplayName"] ?? "External provider";
    public Task<string> CreateChallengeUrlAsync(string returnUrl, CancellationToken cancellationToken = default)
    {
        var state = stateStore.Create(returnUrl);
        var query = $"client_id={Uri.EscapeDataString(configuration["OAuth:ClientId"] ?? "")}&redirect_uri={Uri.EscapeDataString(configuration["OAuth:RedirectUri"] ?? "")}&response_type=code&scope={Uri.EscapeDataString(configuration["OAuth:Scope"] ?? "openid profile email")}&state={Uri.EscapeDataString(state)}";
        return Task.FromResult($"{configuration["OAuth:AuthorizationEndpoint"]}?{query}");
    }
    public async Task<ExternalLoginIdentity?> ResolveIdentityAsync(string callbackCode, string state, CancellationToken cancellationToken = default)
    {
        var tokenResponse = await clients.CreateClient().PostAsync(configuration["OAuth:TokenEndpoint"], new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = callbackCode, ["client_id"] = configuration["OAuth:ClientId"] ?? "", ["client_secret"] = configuration["OAuth:ClientSecret"] ?? "", ["redirect_uri"] = configuration["OAuth:RedirectUri"] ?? "", ["grant_type"] = "authorization_code" }), cancellationToken);
        tokenResponse.EnsureSuccessStatusCode(); var token = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("access_token").GetString();
        var profile = await clients.CreateClient().GetFromJsonAsync<JsonElement>(configuration["OAuth:UserInfoEndpoint"], cancellationToken); var key = profile.GetProperty(configuration["OAuth:SubjectClaim"] ?? "sub").GetString(); var email = profile.GetProperty(configuration["OAuth:EmailClaim"] ?? "email").GetString();
        return key is null || email is null ? null : new ExternalLoginIdentity(key, email, profile.TryGetProperty(configuration["OAuth:NameClaim"] ?? "name", out var name) ? name.GetString() : null);
    }
}