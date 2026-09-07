using System.Security.Claims;
using V080ProductionCheck.Api.Authorization;
using V080ProductionCheck.Api.Auditing;
using V080ProductionCheck.Api.Data;
using V080ProductionCheck.Api.Identity;
using V080ProductionCheck.Api.Integrations;
using V080ProductionCheck.Api.Infrastructure;
using V080ProductionCheck.Api.Jobs;
using V080ProductionCheck.Api.Features.Health;
using V080ProductionCheck.Api.Tenancy;
using Wolverine;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;

if (TryExportDotisanContract(args))
    return;

var builder = WebApplication.CreateBuilder(args);
if (args.Contains("--dotisan-observability", StringComparer.OrdinalIgnoreCase))
{
    builder.Configuration["OpenTelemetry:Enabled"] = "true";
}
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? context.HttpContext.TraceIdentifier;
});
var openTelemetry = builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation());
builder.Logging.AddOpenTelemetry(logging => logging.IncludeFormattedMessage = true);
if (builder.Configuration.GetValue("OpenTelemetry:Enabled", false))
{
    openTelemetry.UseOtlpExporter();
}
var connectionString = ResolveConnectionString(builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required."), builder.Environment.ContentRootPath);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
builder.Services.AddScoped<IAuditWriter, AuditWriter>();
builder.Services.AddSignalR();
    builder.Services.AddScoped<INotificationStore, EfNotificationStore>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<IDistributedApplicationCache, MemoryApplicationCache>();
    builder.Services.AddSingleton<IApplicationCache>(services => services.GetRequiredService<IDistributedApplicationCache>());
builder.Services.AddScoped<IDataExchangeService, DataExchangeService>();
builder.Services.AddHttpClient();
    builder.Services.AddScoped<IWebhookDispatcher, HmacWebhookDispatcher>();
builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Host.UseWolverine(opts => JobRegistration.Configure(opts, connectionString, builder.Configuration));
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 8;
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddRoles<IdentityRole>()
.AddSignInManager()
.AddDefaultTokenProviders()
.AddEntityFrameworkStores<AppDbContext>();
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IExternalLoginStateStore, ExternalLoginStateStore>();
var mailProvider = builder.Configuration["Mail:Provider"]?.ToLowerInvariant() ?? "console";
if (mailProvider == "mailpit" && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Mailpit is only supported in the Development environment. Select smtp for staging or production.");
builder.Services.AddSingleton<IEmailProvider>(services =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    return mailProvider switch
    {
        "mailpit" => new MailpitEmailSender(services.GetRequiredService<IHttpClientFactory>(), configuration),
        "smtp" => new SmtpEmailSender(configuration),
        _ => new ConsoleEmailSender()
    };
});
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = async context =>
        {
            var value = context.Principal?.FindFirstValue("dotisan_session_id");
            if (!Guid.TryParse(value, out var sessionId)) return;
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var session = await db.ApplicationSessions.SingleOrDefaultAsync(item => item.Id == sessionId);
            var now = DateTimeOffset.UtcNow;
            if (session is null || session.RevokedAt is not null || session.ExpiresAt <= now) { context.RejectPrincipal(); return; }
            if (session.LastSeenAt < now.AddMinutes(-5)) { session.LastSeenAt = now; await db.SaveChangesAsync(); }
        };
    })
    .AddCookie(IdentityConstants.TwoFactorUserIdScheme)
    .AddCookie(IdentityConstants.TwoFactorRememberMeScheme);
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in Permissions.All)
    {
        options.AddPolicy(permission, policy =>
            policy.RequireClaim(Permissions.ClaimType, permission));
    }
});
builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");
builder.Services.AddRateLimiter(options => options.AddPolicy("account", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));

var app = builder.Build();
app.Use(async (context, next) =>
{
    var supplied = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    var correlationId = !string.IsNullOrWhiteSpace(supplied) && supplied.Length <= 100 && supplied.All(character => char.IsLetterOrDigit(character) || character is '-' or '_')
        ? supplied
        : Guid.NewGuid().ToString("N");
    context.Request.Headers["X-Correlation-ID"] = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    await next(context);
});
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseDefaultFiles();
app.UseStaticFiles();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();
app.MapDotisanEndpoints();
app.MapFallbackToFile("index.html");
app.Run();

static string ResolveConnectionString(string configured, string contentRoot)
{
    if (!configured.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
        return configured;
    var parts = configured.Split(';');
    var dataSource = parts[0]["Data Source=".Length..].Trim();
    if (Path.IsPathRooted(dataSource) || dataSource.Contains('|'))
        return configured;
    var resolvedPath = Path.Combine(contentRoot, dataSource);
    Directory.CreateDirectory(Path.GetDirectoryName(resolvedPath)!);
    parts[0] = $"Data Source={resolvedPath}";
    return string.Join(';', parts);
}

static bool TryExportDotisanContract(string[] arguments)
{
    const string option = "--dotisan-export-contract";
    var index = Array.IndexOf(arguments, option);
    if (index < 0)
        return false;
    if (index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index + 1]))
        throw new InvalidOperationException($"{option} requires an output path.");

    var outputPath = Path.GetFullPath(arguments[index + 1]);
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    File.WriteAllText(outputPath, Dotisan.Generated.DotisanContractExport.ContractManifestJson);
    return true;
}

public partial class Program { }