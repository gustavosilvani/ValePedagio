using Microsoft.Extensions.Logging;
using ValePedagio.Domain;

namespace ValePedagio.Infrastructure.Providers;

public sealed class DbTransValePedagioProvider : IValePedagioProvider
{
    private readonly IValePedagioProviderConfigurationRepository _repo;
    private readonly DbTransHttpClient _client;

    public DbTransValePedagioProvider(IValePedagioProviderConfigurationRepository repo, DbTransHttpClient client)
    {
        _repo = repo;
        _client = client;
    }

    public ValePedagioProviderDescriptor Descriptor =>
        ValePedagioProviderCatalog.Descriptors.Single(d => d.Type == ValePedagioProviderType.DBTrans);

    public async Task<ValePedagioProviderOperationResult> QuoteAsync(ValePedagioProviderOperationContext ctx, CancellationToken ct = default)
        => await _client.QuoteAsync(await Settings(ctx.TenantId, ct), ctx, ct);

    public async Task<ValePedagioProviderOperationResult> PurchaseAsync(ValePedagioProviderOperationContext ctx, CancellationToken ct = default)
        => await _client.PurchaseAsync(await Settings(ctx.TenantId, ct), ctx, null, ct);

    public async Task<ValePedagioProviderOperationResult> PurchaseAsync(ValePedagioSolicitacao sol, CancellationToken ct = default)
        => await _client.PurchaseAsync(await Settings(sol.TenantId, ct), null, sol, ct);

    public async Task<ValePedagioProviderOperationResult> SyncAsync(ValePedagioSolicitacao sol, CancellationToken ct = default)
        => await _client.SyncAsync(await Settings(sol.TenantId, ct), sol, ct);

    public async Task<ValePedagioProviderOperationResult> CancelAsync(ValePedagioSolicitacao sol, CancellationToken ct = default)
        => await _client.CancelAsync(await Settings(sol.TenantId, ct), sol, ct);

    public async Task<ValePedagioReceipt?> GetReceiptAsync(ValePedagioSolicitacao sol, CancellationToken ct = default)
        => await _client.GetReceiptAsync(await Settings(sol.TenantId, ct), sol, ct);

    private async Task<RestProviderSettings> Settings(string tenantId, CancellationToken ct)
    {
        var config = await _repo.GetAsync(tenantId, ValePedagioProviderType.DBTrans, ct);
        var c = config.Credentials;
        var timeout = int.TryParse(RestProviderSettings.G(c, "timeoutSeconds"), out var t) ? t : 30;
        return new RestProviderSettings(
            config.EndpointBaseUrl,
            RestProviderSettings.G(c, "clientId") ?? string.Empty,
            RestProviderSettings.G(c, "apiKey") ?? string.Empty,
            RestProviderSettings.G(c, "providerDocument") ?? ValePedagioProviderDocuments.Documents[ValePedagioProviderType.DBTrans],
            RestProviderSettings.G(c, "documentType") ?? "Cartao",
            TimeSpan.FromSeconds(Math.Clamp(timeout, 5, 120)));
    }
}

// DBTrans uses Bearer auth (base64 clientId:apiKey). No callback — polling only.
public sealed class DbTransHttpClient
{
    private const string ProviderName = "DBTrans";
    private readonly RestValePedagioHttpClient _rest;

    public DbTransHttpClient(HttpClient http, ILogger<DbTransHttpClient> logger)
        => _rest = new RestValePedagioHttpClient(http, logger);

    public Task<ValePedagioProviderOperationResult> QuoteAsync(RestProviderSettings s, ValePedagioProviderOperationContext ctx, CancellationToken ct)
        => _rest.QuoteAsync(s, ctx, ProviderName, ct);

    public Task<ValePedagioProviderOperationResult> PurchaseAsync(RestProviderSettings s, ValePedagioProviderOperationContext? ctx, ValePedagioSolicitacao? sol, CancellationToken ct)
        => _rest.PurchaseAsync(s, ctx, sol, ProviderName, callbackMode: false, ct);

    public Task<ValePedagioProviderOperationResult> SyncAsync(RestProviderSettings s, ValePedagioSolicitacao sol, CancellationToken ct)
        => _rest.SyncAsync(s, sol, ProviderName, ct);

    public Task<ValePedagioProviderOperationResult> CancelAsync(RestProviderSettings s, ValePedagioSolicitacao sol, CancellationToken ct)
        => _rest.CancelAsync(s, sol, ProviderName, ct);

    public Task<ValePedagioReceipt?> GetReceiptAsync(RestProviderSettings s, ValePedagioSolicitacao sol, CancellationToken ct)
        => _rest.GetReceiptAsync(s, sol, ProviderName, ct);
}
