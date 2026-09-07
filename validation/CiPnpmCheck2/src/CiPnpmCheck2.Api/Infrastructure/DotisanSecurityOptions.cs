using System.ComponentModel.DataAnnotations;

namespace CiPnpmCheck2.Api.Infrastructure;

public sealed class DotisanSecurityOptions
{
    [Url]
    public string? FrontendUrl { get; set; }

    public string? DataProtectionKeyDirectory { get; set; }

    public string[] KnownProxies { get; set; } = [];
}