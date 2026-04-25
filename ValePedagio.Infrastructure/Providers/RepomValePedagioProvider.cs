using Microsoft.Extensions.Logging;
using ValePedagio.Domain;

namespace ValePedagio.Infrastructure.Providers;

public sealed class RepomValePedagioProvider : IValePedagioProvider
{
    private readonly IValePedagioProviderConfigurationRepository _repo;
    private readonly RepomHttpClient _client;

    public RepomValePedagioProvider(IValePedagioProviderConfigurationRepository repo, RepomHttpClient client)
    {
        _repo = repo;
        _client = client;
    }

    public ValePedagioProviderDescriptor Descriptor =>
        ValePedagioProviderCatalog.Descriptors.Single(d => d.Type == ValePedagioProviderType.Repom);

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
        var config = await _repo.GetAsync(tenantId, ValePedagioProviderType.Repom, ct);
        var c = config.Credentials;
        var timeout = int.TryParse(RestProviderSettings.G(c, "timeoutSeconds"), out var t) ? t : 30;
        return new RestProviderSettings(
            config.EndpointBaseUrl,
            RestProviderSettings.G(c, "clientId") ?? string.Empty,
            RestProviderSettings.G(c, "apiKey") ?? string.Empty,
            RestProviderSettings.G(c, "providerDocument") ?? ValePedagioProviderDocuments.Documents[ValePedagioProviderType.Repom],
            RestProviderSettings.G(c, "documentType") ?? "Cartao",
            TimeSpan.FromSeconds(Math.Clamp(timeout, 5, 120)),
            RetryEnabled: true);
    }
}

// Repom uses Bearer auth with retry for transient failures.
public sealed class RepomHttpClient
{
    private const string ProviderName = "Repom";
    private readonly RestValePedagioHttpClient _rest;

    public RepomHttpClient(HttpClient http, ILogger<RepomHttpClient> logger)
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
