using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace V080ShowcaseVerified.Api.Tenancy;

public interface ITenantContext
{
    string TenantId { get; }
    string RequireTenantId();
}

public interface ITenantEntity
{
    string TenantId { get; set; }
}

public sealed class TenantContext(IHttpContextAccessor httpContextAccessor, IHostEnvironment environment) : ITenantContext
{
    public string TenantId
    {
        get
        {
            var claimTenant = httpContextAccessor.HttpContext?.User.FindFirstValue("tenant_id")?.Trim();
            if (!string.IsNullOrWhiteSpace(claimTenant))
                return claimTenant;

            var headerTenant = environment.IsDevelopment()
                ? httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-ID"].FirstOrDefault()?.Trim()
                : null;
            return !string.IsNullOrWhiteSpace(headerTenant)
                ? headerTenant
                : environment.IsEnvironment("Testing")
                    ? "test-tenant"
                : throw new InvalidOperationException("A tenant_id claim is required.");
        }
    }

    public string RequireTenantId() => TenantId;
}