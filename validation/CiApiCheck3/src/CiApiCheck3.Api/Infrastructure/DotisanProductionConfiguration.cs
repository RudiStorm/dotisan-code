using Microsoft.Extensions.Configuration;

namespace CiApiCheck3.Api.Infrastructure;

public static class DotisanProductionConfiguration
{
    public static void Validate(IConfiguration configuration, string environmentName, bool emailConfirmationEnabled = false)
    {
        if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase))
            return;

        var frontendUrl = configuration["Dotisan:Security:FrontendUrl"] ?? configuration["FrontendUrl"];
        if (!Uri.TryCreate(frontendUrl, UriKind.Absolute, out var parsedFrontendUrl) || parsedFrontendUrl.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Dotisan:Security:FrontendUrl must be an absolute HTTPS URL outside Development.");

        var keyDirectory = configuration["Dotisan:Security:DataProtectionKeyDirectory"] ?? configuration["DataProtection:KeyDirectory"];
        if (string.IsNullOrWhiteSpace(keyDirectory))
            throw new InvalidOperationException("Dotisan:Security:DataProtectionKeyDirectory is required outside Development.");

        if (emailConfirmationEnabled)
        {
            var mailProvider = configuration["Mail:Provider"]?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(mailProvider) || mailProvider is "console" or "mailpit")
                throw new InvalidOperationException("Mail:Provider must be smtp or a custom provider outside Development.");
        }
    }
}