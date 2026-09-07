using CiApiCheck3.Api.Auditing;

using Microsoft.EntityFrameworkCore;
using CiApiCheck3.Api.Data;
using CiApiCheck3.Api.Infrastructure;
using CiApiCheck3.Api.Jobs;
using CiApiCheck3.Api.Features.Health;

using Wolverine;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

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
    DotisanProductionConfiguration.Validate(builder.Configuration, builder.Environment.EnvironmentName, emailConfirmationEnabled: false);
    var keyDirectory = builder.Configuration["Dotisan:Security:DataProtectionKeyDirectory"] ?? builder.Configuration["DataProtection:KeyDirectory"];
    var resolvedKeyDirectory = Path.GetFullPath(keyDirectory!, builder.Environment.ContentRootPath);
    Directory.CreateDirectory(resolvedKeyDirectory);
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(resolvedKeyDirectory));
}
builder.Services.AddScoped<IAuditWriter, AuditWriter>();






builder.Host.UseWolverine(opts => JobRegistration.Configure(opts, connectionString, builder.Configuration));

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