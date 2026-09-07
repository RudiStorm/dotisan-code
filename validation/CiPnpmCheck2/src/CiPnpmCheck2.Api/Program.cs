using System.Security.Claims;
using CiPnpmCheck2.Api.Authorization;
using CiPnpmCheck2.Api.Auditing;
using CiPnpmCheck2.Api.Data;
using CiPnpmCheck2.Api.Identity;
using CiPnpmCheck2.Api.Integrations;
using CiPnpmCheck2.Api.Infrastructure;
using CiPnpmCheck2.Api.Jobs;
using CiPnpmCheck2.Api.Features.Health;
using Microsoft.AspNetCore.DataProtection;

using Wolverine;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;

if (TryExportDotisanContract(args))
    return;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOptions<DotisanSecurityOptions>()
    .BindConfiguration("Dotisan:Security")
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var value in builder.Configuration.GetSection("Dotisan:Security:KnownProxies").Get<string[]>() ?? [])
    {
        if (IPAddress.TryParse(value, out var address))
            options.KnownProxies.Add(address);
    }
});
builder.Services.AddCors(options => options.AddPolicy("frontend", policy =>
{
    var frontendUrl = builder.Configuration["Dotisan:Security:FrontendUrl"] ?? builder.Configuration["FrontendUrl"];
    if (Uri.TryCreate(frontendUrl, UriKind.Absolute, out var origin))
        policy.WithOrigins(origin.GetLeftPart(UriPartial.Authority)).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));
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
if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
{
    DotisanProductionConfiguration.Validate(builder.Configuration, builder.Environment.EnvironmentName, emailConfirmationEnabled: true);
    var keyDirectory = builder.Configuration["Dotisan:Security:DataProtectionKeyDirectory"] ?? builder.Configuration["DataProtection:KeyDirectory"];
    var resolvedKeyDirectory = Path.GetFullPath(keyDirectory!, builder.Environment.ContentRootPath);
    Directory.CreateDirectory(resolvedKeyDirectory);
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(resolvedKeyDirectory));
}
builder.Services.AddScoped<IAuditWriter, AuditWriter>();






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
if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing") && mailProvider == "console")
    throw new InvalidOperationException("Mail:Provider must be smtp or a custom provider outside Development.");
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
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
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
app.UseForwardedHeaders();
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
app.UseCors("frontend");
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