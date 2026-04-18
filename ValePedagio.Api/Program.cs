using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ValePedagio.Application;
using ValePedagio.Domain;
using ValePedagio.Infrastructure;
using ValePedagio.Infrastructure.Persistence;
using ValePedagio.Infrastructure.Providers;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddMemoryCache();
builder.Services.AddScoped<IValePedagioSolicitacaoRepository, PostgresValePedagioSolicitacaoRepository>();
builder.Services.AddScoped<IValePedagioProviderConfigurationRepository, PostgresValePedagioProviderConfigurationRepository>();
builder.Services.AddScoped<IValePedagioProviderResolver, ValePedagioProviderResolver>();
builder.Services.AddScoped<IValePedagioApplicationService, ValePedagioApplicationService>();

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
app.MapControllers();
app.MapGet("/", () => Results.Ok(new
{
    service = "ValePedagio.Api",
    status = "ok",
    version = "v1"
}));

app.Run();

public partial class Program;
