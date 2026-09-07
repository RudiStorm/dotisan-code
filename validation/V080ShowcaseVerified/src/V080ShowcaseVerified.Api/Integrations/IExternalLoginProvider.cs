using Microsoft.AspNetCore.DataProtection;
using System.Collections.Concurrent;

namespace V080ShowcaseVerified.Api.Integrations;

public sealed record ExternalLoginProviderDescriptor(string Name, string DisplayName);
public sealed record ExternalLoginIdentity(string ProviderKey, string Email, string? DisplayName);
public interface IExternalLoginStateStore
{
    string Create(string returnUrl);
    bool TryConsume(string state, out string returnUrl);
}

public sealed class ExternalLoginStateStore(IDataProtectionProvider dataProtectionProvider) : IExternalLoginStateStore
{
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("Dotisan.ExternalLogin.State");
    private readonly ConcurrentDictionary<string, byte> consumed = new(StringComparer.Ordinal);

    public string Create(string returnUrl) => protector.Protect($"{Guid.NewGuid():N}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}|{returnUrl}");

    public bool TryConsume(string state, out string returnUrl)
    {
        returnUrl = "/";
        try
        {
            var parts = protector.Unprotect(state).Split('|', 3);
            if (parts.Length != 3 || !long.TryParse(parts[1], out var createdAt) || DateTimeOffset.UtcNow.ToUnixTimeSeconds() - createdAt > 600 || !consumed.TryAdd(parts[0], 0)) return false;
            returnUrl = parts[2];
            return true;
        }
        catch (Exception) { return false; }
    }
}

public interface IExternalLoginProvider
{
    string Name { get; }
    string DisplayName { get; }
    Task<string> CreateChallengeUrlAsync(string returnUrl, CancellationToken cancellationToken = default);
    Task<ExternalLoginIdentity?> ResolveIdentityAsync(string callbackCode, string state, CancellationToken cancellationToken = default);
}