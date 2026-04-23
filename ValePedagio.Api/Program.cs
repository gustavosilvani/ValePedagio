using System.Text.Json.Serialization;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using ValePedagio.Api;
using ValePedagio.Application;
using ValePedagio.Domain;
using ValePedagio.Infrastructure;
using ValePedagio.Infrastructure.Persistence;
using ValePedagio.Infrastructure.Providers;

var builder = WebApplication.CreateBuilder(args);
Activity.DefaultIdFormat = ActivityIdFormat.W3C;
Activity.ForceDefaultIdFormat = true;

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("service.name", builder.Environment.ApplicationName)
        .WriteTo.Console();
});

var serviceName = builder.Environment.ApplicationName;
var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";
var otlpEndpoint = builder.Configuration["OpenTelemetry:Otlp:Endpoint"]
    ?? builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var corsAllowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

if (corsAllowedOrigins.Length == 0)
{
    corsAllowedOrigins =
    [
        "http://localhost:4200",
        "http://localhost:4201",
        "http://localhost:4202"
    ];
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("ValePedagioSpa", policy =>
    {
        policy
            .WithOrigins(corsAllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = "Host=localhost;Port=5432;Database=ValePedagio;Username=postgres;Password=postgres";
}

builder.Services.AddDbContext<ValePedagioDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "database", tags: new[] { "db", "ready" })
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { "live" });

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: serviceName,
        serviceVersion: serviceVersion,
        serviceInstanceId: Environment.MachineName))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter();

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    });

builder.Services.AddMemoryCache();
builder.Services.AddScoped<IValePedagioSolicitacaoRepository, PostgresValePedagioSolicitacaoRepository>();
builder.Services.AddScoped<IValePedagioProviderConfigurationRepository, PostgresValePedagioProviderConfigurationRepository>();
builder.Services.AddScoped<IValePedagioProviderResolver, ValePedagioProviderResolver>();
builder.Services.AddScoped<IValePedagioApplicationService, ValePedagioApplicationService>();
builder.Services.AddHostedService<ValePedagioPendingSyncHostedService>();

builder.Services.AddHttpClient<EFreteSoapClient>(client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
});

builder.Services.AddScoped<IValePedagioProvider, EFreteValePedagioProvider>();
foreach (var descriptor in ValePedagioProviderCatalog.Descriptors.Where(item => item.Type != ValePedagioProviderType.EFrete))
{
    builder.Services.AddScoped<IValePedagioProvider>(_ => new CatalogValePedagioProvider(descriptor));
}

var app = builder.Build();

if (builder.Configuration.GetValue("ValePedagio:AutoApplyMigrations", false))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ValePedagioDbContext>();
    dbContext.Database.Migrate();
}

app.UseCors("ValePedagioSpa");
app.UseSerilogRequestLogging();
app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics");
app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapGet("/", () => Results.Ok(new
{
    service = "ValePedagio.Api",
    status = "ok",
    version = "v1"
}));

app.Run();

public partial class Program;
