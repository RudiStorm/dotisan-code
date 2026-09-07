using System.ComponentModel.DataAnnotations;

namespace GeneratedAppintegrationsCheck.Api.Infrastructure;

public sealed class DotisanSecurityOptions
{
    [Url]
    public string? FrontendUrl { get; set; }

    public string? DataProtectionKeyDirectory { get; set; }

    public string[] KnownProxies { get; set; } = [];
}