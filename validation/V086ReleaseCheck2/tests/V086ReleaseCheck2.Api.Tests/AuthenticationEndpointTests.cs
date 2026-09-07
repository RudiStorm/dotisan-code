using System.Security.Claims;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using V086ReleaseCheck2.Api.Authorization;
using V086ReleaseCheck2.Api.Auditing;
using V086ReleaseCheck2.Api.Data;
using V086ReleaseCheck2.Api.Identity;
using V086ReleaseCheck2.Api.Integrations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;

namespace V086ReleaseCheck2.Api.Tests;

public sealed class AuthenticationEndpointTests(AuthenticationApplicationFactory factory) : IClassFixture<AuthenticationApplicationFactory>
{
    [Fact]
    public async Task Liveness_and_readiness_health_endpoints_are_available()
    {
        using var client = factory.CreateClient();
        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Fact]
    public async Task Correlation_id_is_returned_on_api_responses()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/account/me");
        request.Headers.Add("X-Correlation-ID", "health-correlation");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("health-correlation", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task Me_requires_authentication()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/account/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_requires_authentication_and_permission()
    {
        using var anonymousClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymousClient.GetAsync("/api/authorization/profile")).StatusCode);

        using var client = await SignInAsync($"no-permission-{Guid.NewGuid():N}@example.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/authorization/profile")).StatusCode);
    }

    [Fact]
    public async Task Role_permission_allows_profile()
    {
        using var client = await SignInAsync($"permission-{Guid.NewGuid():N}@example.com", grantProfilePermission: true);

        var response = await client.GetAsync("/api/authorization/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(Permissions.ProfileView, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_login_does_not_reveal_account_existence()
    {
        using var client = factory.CreateClient();
        var antiforgery = await GetAntiforgeryToken(client);
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/account/login");
        login.Headers.Add("X-XSRF-TOKEN", antiforgery);
        login.Content = JsonContent.Create(new { email = "missing@example.com", password = "wrong", rememberMe = false });

        var response = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Newly_registered_unconfirmed_users_cannot_access_protected_resources()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var antiforgery = await GetAntiforgeryToken(client);
        using var register = new HttpRequestMessage(HttpMethod.Post, "/api/account/register");
        register.Headers.Add("X-XSRF-TOKEN", antiforgery);
        register.Content = JsonContent.Create(new { email = $"unconfirmed-{Guid.NewGuid():N}@example.com", password = "Password1!" });

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(register)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);
    }

    [Fact]
    public async Task Successful_login_writes_actor_trace_and_correlation()
    {
        var email = $"audit-login-{Guid.NewGuid():N}@example.com";
        await factory.SeedUserAsync(email, grantProfilePermission: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var antiforgery = await GetAntiforgeryToken(client);
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/account/login");
        login.Headers.Add("X-XSRF-TOKEN", antiforgery);
        login.Headers.Add("X-Correlation-ID", "correlation-test");
        login.Content = JsonContent.Create(new { email, password = "Password1!", rememberMe = false });

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(login)).StatusCode);
        var entry = (await factory.ReadAuditEntriesAsync()).Single(item => item.Action == "security.login.succeeded" && item.CorrelationId == "correlation-test");
        Assert.Equal("Security", entry.EntityType);
        Assert.False(string.IsNullOrWhiteSpace(entry.ActorId));
        Assert.False(string.IsNullOrWhiteSpace(entry.TraceId));
    }

    [Fact]
    public async Task Failed_login_is_audited_without_credentials()
    {
        var correlationId = $"failed-{Guid.NewGuid():N}";
        using var client = factory.CreateClient();
        var antiforgery = await GetAntiforgeryToken(client);
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/account/login");
        login.Headers.Add("X-XSRF-TOKEN", antiforgery);
        login.Headers.Add("X-Correlation-ID", correlationId);
        login.Content = JsonContent.Create(new { email = "missing-audit@example.com", password = "do-not-store-this", rememberMe = false });

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(login)).StatusCode);
        var entry = (await factory.ReadAuditEntriesAsync()).Single(item => item.Action == "security.login.failed" && item.CorrelationId == correlationId);
        Assert.DoesNotContain("do-not-store-this", entry.Changes, StringComparison.Ordinal);
        Assert.DoesNotContain("missing-audit@example.com", entry.Changes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Disabled_audit_configuration_skips_writes()
    {
        using var disabledFactory = new AuthenticationApplicationFactory { AuditEnabled = false };
        using var client = disabledFactory.CreateClient();
        var antiforgery = await GetAntiforgeryToken(client);
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/account/login");
        login.Headers.Add("X-XSRF-TOKEN", antiforgery);
        login.Content = JsonContent.Create(new { email = "disabled-audit@example.com", password = "do-not-store-this", rememberMe = false });

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(login)).StatusCode);
        Assert.Empty(await disabledFactory.ReadAuditEntriesAsync());
    }

    [Fact]
    public async Task Mfa_setup_requires_authentication_and_invalid_codes_are_rejected()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/account/mfa/setup")).StatusCode);
        using var client = await SignInAsync($"mfa-{Guid.NewGuid():N}@example.com");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/account/mfa/setup")).StatusCode);
        var antiforgery = await GetAntiforgeryToken(client);
        using var verify = new HttpRequestMessage(HttpMethod.Post, "/api/account/mfa/verify");
        verify.Headers.Add("X-XSRF-TOKEN", antiforgery);
        verify.Content = JsonContent.Create(new { code = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(verify)).StatusCode);
    }

    [Fact]
    public async Task Recovery_code_completes_the_mfa_challenge_once()
    {
        var email = $"recovery-{Guid.NewGuid():N}@example.com";
        var recoveryCode = await factory.SeedMfaUserAsync(email);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var antiforgery = await GetAntiforgeryToken(client);
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/account/login");
        login.Headers.Add("X-XSRF-TOKEN", antiforgery);
        login.Content = JsonContent.Create(new { email, password = "Password1!", rememberMe = false });
        var loginResponse = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Contains("mfa_required", await loginResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        antiforgery = await GetAntiforgeryToken(client);
        using var challenge = new HttpRequestMessage(HttpMethod.Post, "/api/account/mfa/challenge");
        challenge.Headers.Add("X-XSRF-TOKEN", antiforgery);
        challenge.Content = JsonContent.Create(new { code = recoveryCode });
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(challenge)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/account/me")).StatusCode);
    }

    [Fact]
    public async Task Session_can_be_listed_and_revoked()
    {
        using var client = await SignInAsync($"session-{Guid.NewGuid():N}@example.com");
        var sessionsResponse = await client.GetAsync("/api/account/sessions");
        Assert.True(sessionsResponse.IsSuccessStatusCode, await sessionsResponse.Content.ReadAsStringAsync());
        var payload = JsonSerializer.Deserialize<JsonElement>(await sessionsResponse.Content.ReadAsStringAsync());
        var sessionId = payload.GetProperty("sessions")[0].GetProperty("id").GetGuid();
        var antiforgery = await GetAntiforgeryToken(client);
        using var revoke = new HttpRequestMessage(HttpMethod.Delete, $"/api/account/sessions/{sessionId}");
        revoke.Headers.Add("X-XSRF-TOKEN", antiforgery);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(revoke)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);
    }

    [Fact]
    public async Task Email_confirmation_rejects_invalid_tokens_without_revealing_details()
    {
        using var client = factory.CreateClient();
        var antiforgery = await GetAntiforgeryToken(client);
        using var confirmation = new HttpRequestMessage(HttpMethod.Post, "/api/account/email-confirmation/confirm");
        confirmation.Headers.Add("X-XSRF-TOKEN", antiforgery);
        confirmation.Content = JsonContent.Create(new { email = "missing@example.com", token = "invalid" });
        var response = await client.SendAsync(confirmation);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_confirmation", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task External_provider_listing_is_safe_when_no_adapter_is_registered()
    {
        using var client = factory.CreateClient();
        var providers = await client.GetFromJsonAsync<JsonElement>("/api/account/external/providers");
        Assert.Empty(providers.GetProperty("providers").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/account/external/google/challenge")).StatusCode);
    }

    [Fact]
    public void External_login_state_is_protected_and_single_use()
    {
        var stateStore = factory.Services.GetRequiredService<IExternalLoginStateStore>();
        var state = stateStore.Create("/security/external-logins");

        Assert.DoesNotContain("/security/external-logins", state, StringComparison.Ordinal);
        Assert.True(stateStore.TryConsume(state, out var returnUrl));
        Assert.Equal("/security/external-logins", returnUrl);
        Assert.False(stateStore.TryConsume(state, out _));
        Assert.False(stateStore.TryConsume("invalid-state", out _));
    }

    [Fact]
    public async Task Fake_external_provider_completes_login_and_rejects_replayed_state()
    {
        using var client = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IExternalLoginProvider, FakeExternalLoginProvider>()))
            .CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var challenge = await client.GetAsync("/api/account/external/fake/challenge?returnUrl=%2Fsecurity%2Fexternal-logins");
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var callbackUrl = challenge.Headers.Location?.ToString() ?? throw new InvalidOperationException("Fake provider did not return a callback URL.");

        var callback = await client.GetAsync(callbackUrl);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/security/external-logins", callback.Headers.Location?.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(callbackUrl)).StatusCode);
    }

    private sealed class FakeExternalLoginProvider(IExternalLoginStateStore stateStore) : IExternalLoginProvider
    {
        public string Name => "fake";
        public string DisplayName => "Fake provider";
        public Task<string> CreateChallengeUrlAsync(string returnUrl, CancellationToken cancellationToken = default)
            => Task.FromResult($"/api/account/external/fake/callback?code=fake-code&state={Uri.EscapeDataString(stateStore.Create(returnUrl))}");
        public Task<ExternalLoginIdentity?> ResolveIdentityAsync(string callbackCode, string state, CancellationToken cancellationToken = default)
            => Task.FromResult<ExternalLoginIdentity?>(callbackCode == "fake-code" ? new ExternalLoginIdentity("fake-user", "fake-user@example.com", "Fake User") : null);
    }

    [Fact]
    public async Task Vendor_mail_adapters_send_requests_without_logging_secrets()
    {
        var handler = new RecordingHandler();
        var clients = new RecordingHttpClientFactory(handler);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mail:SendGrid:ApiKey"] = "sendgrid-secret",
            ["Mail:SendGrid:From"] = "no-reply@example.com",
            ["Mail:Mailgun:ApiKey"] = "mailgun-secret",
            ["Mail:Mailgun:Domain"] = "example.test",
            ["Mail:Mailgun:From"] = "no-reply@example.com"
        }).Build();

        await new SendGridEmailProvider(clients, configuration).SendAsync("person@example.com", "Subject", "Body");
        Assert.Equal("https://api.sendgrid.com/v3/mail/send", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("Bearer sendgrid-secret", handler.LastRequest.Headers.Authorization!.ToString());
        await new MailgunEmailProvider(clients, configuration).SendAsync("person@example.com", "Subject", "Body");
        Assert.Equal("https://api.mailgun.net/v3/example.test/messages", handler.LastRequest.RequestUri!.ToString());
        Assert.StartsWith("Basic ", handler.LastRequest.Headers.Authorization!.ToString(), StringComparison.Ordinal);
    }

    private sealed class RecordingHttpClientFactory(RecordingHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        }
    }

        [Fact]
    public async Task Public_registration_requires_email_confirmation_before_protected_access()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var antiforgery = await GetAntiforgeryToken(client);
        using var register = new HttpRequestMessage(HttpMethod.Post, "/api/account/register");
        register.Headers.Add("X-XSRF-TOKEN", antiforgery);
        register.Content = JsonContent.Create(new { email = "person@example.com", password = "Password1!" });

        var registration = await client.SendAsync(register);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);
    }

    [Fact]
    public async Task Registration_email_contains_a_confirmable_frontend_link()
    {
        var email = $"confirm-{Guid.NewGuid():N}@example.com";
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var antiforgery = await GetAntiforgeryToken(client);
        using var register = new HttpRequestMessage(HttpMethod.Post, "/api/account/register");
        register.Headers.Add("X-XSRF-TOKEN", antiforgery);
        register.Content = JsonContent.Create(new { email, password = "Password1!" });

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(register)).StatusCode);
        var link = factory.EmailProvider.LastBody.Split('\n', StringSplitOptions.RemoveEmptyEntries).Single(line => line.Contains("/auth/confirm-email?", StringComparison.Ordinal)).Trim();
        var query = link[(link.IndexOf('?') + 1)..].Split('&').Select(part => part.Split('=', 2)).ToDictionary(part => part[0], part => Uri.UnescapeDataString(part[1]));
        using var confirmation = new HttpRequestMessage(HttpMethod.Post, "/api/account/email-confirmation/confirm");
        confirmation.Headers.Add("X-XSRF-TOKEN", await GetAntiforgeryToken(client));
        confirmation.Content = JsonContent.Create(new { email = query["email"], token = query["token"] });

        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(confirmation)).StatusCode);
    }

    [Fact]
    public async Task Logout_clears_the_authentication_cookie()
    {
        using var client = await SignInAsync("logout@example.com");

        var antiforgery = await GetAntiforgeryToken(client);
        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/account/logout");
        logout.Headers.Add("X-XSRF-TOKEN", antiforgery);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(logout)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);
    }

    private static async Task<string> GetAntiforgeryToken(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<AntiforgeryResponse>("/api/account/antiforgery");
        return response?.Token ?? throw new InvalidOperationException("The generated antiforgery endpoint returned no token.");
    }

    private async Task<HttpClient> SignInAsync(string email, bool grantProfilePermission = false)
    {
        await factory.SeedUserAsync(email, grantProfilePermission);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var antiforgery = await GetAntiforgeryToken(client);
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/account/login");
        login.Headers.Add("X-XSRF-TOKEN", antiforgery);
        login.Content = JsonContent.Create(new { email, password = "Password1!", rememberMe = false });
        var response = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return client;
    }

    private sealed record AntiforgeryResponse(string Token);
}

public sealed class AuthenticationApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? connection;
    public bool AuditEnabled { get; set; } = true;
    public CapturingEmailProvider EmailProvider { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Audit:Enabled", AuditEnabled.ToString());
        builder.UseSetting("FrontendUrl", "https://localhost");
        builder.UseSetting("DataProtection:KeyDirectory", Path.Combine(Path.GetTempPath(), "dotisan-test-keys"));
        builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
        builder.ConfigureServices(services =>
        {
            connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddSingleton<IEmailProvider>(EmailProvider);
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        });
    }

    public async Task<IReadOnlyList<AuditEntry>> ReadAuditEntriesAsync()
    {
        using var scope = Services.CreateScope();
        var entries = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditEntries
            .AsNoTracking()
            .ToListAsync();
        return entries.OrderBy(entry => entry.CreatedAt).ToArray();
    }

    public async Task SeedUserAsync(string email, bool grantProfilePermission)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await users.CreateAsync(user, "Password1!");
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }

        if (!grantProfilePermission)
        {
            return;
        }

        var role = new IdentityRole($"AuthorizationTesters-{Guid.NewGuid():N}");
        var roleResult = await roles.CreateAsync(role);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        var claimResult = await roles.AddClaimAsync(role, new Claim(Permissions.ClaimType, Permissions.ProfileView));
        if (!claimResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", claimResult.Errors.Select(error => error.Description)));
        }

        var membershipResult = await users.AddToRoleAsync(user, role.Name!);
        if (!membershipResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", membershipResult.Errors.Select(error => error.Description)));
        }
    }

    public async Task<string> SeedMfaUserAsync(string email)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await users.CreateAsync(user, "Password1!");
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        await users.SetTwoFactorEnabledAsync(user, true);
        return (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 1))?.Single() ?? throw new InvalidOperationException("Recovery code generation failed.");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            connection?.Dispose();
        base.Dispose(disposing);
    }
}

public sealed class CapturingEmailProvider : IEmailProvider
{
    public string LastBody { get; private set; } = string.Empty;

    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        LastBody = body;
        return Task.CompletedTask;
    }
}