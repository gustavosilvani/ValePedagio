using Microsoft.Extensions.Logging;
using ValePedagio.Domain;

namespace ValePedagio.Infrastructure.Providers;

public sealed class SemPararValePedagioProvider : IValePedagioProvider
{
    private readonly IValePedagioProviderConfigurationRepository _repo;
    private readonly SemPararHttpClient _client;

    public SemPararValePedagioProvider(IValePedagioProviderConfigurationRepository repo, SemPararHttpClient client)
    {
        _repo = repo;
        _client = client;
    }

    public ValePedagioProviderDescriptor Descriptor =>
        ValePedagioProviderCatalog.Descriptors.Single(d => d.Type == ValePedagioProviderType.SemParar);

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
        var config = await _repo.GetAsync(tenantId, ValePedagioProviderType.SemParar, ct);
        var c = config.Credentials;
        var timeout = int.TryParse(RestProviderSettings.G(c, "timeoutSeconds"), out var t) ? t : 30;
        // SemParar vales são do tipo TAG (adesivo)
        return new RestProviderSettings(
            config.EndpointBaseUrl,
            RestProviderSettings.G(c, "clientId") ?? string.Empty,
            RestProviderSettings.G(c, "apiKey") ?? string.Empty,
            RestProviderSettings.G(c, "providerDocument") ?? ValePedagioProviderDocuments.Documents[ValePedagioProviderType.SemParar],
            RestProviderSettings.G(c, "documentType") ?? "TAG",
            TimeSpan.FromSeconds(Math.Clamp(timeout, 5, 120)));
    }
}

// SemParar uses TAG type and callback webhook for purchase confirmation.
public sealed class SemPararHttpClient
{
    private const string ProviderName = "SemParar";
    private readonly RestValePedagioHttpClient _rest;

    public SemPararHttpClient(HttpClient http, ILogger<SemPararHttpClient> logger)
        => _rest = new RestValePedagioHttpClient(http, logger);

    public Task<ValePedagioProviderOperationResult> QuoteAsync(RestProviderSettings s, ValePedagioProviderOperationContext ctx, CancellationToken ct)
        => _rest.QuoteAsync(s, ctx, ProviderName, ct);

    public Task<ValePedagioProviderOperationResult> PurchaseAsync(RestProviderSettings s, ValePedagioProviderOperationContext? ctx, ValePedagioSolicitacao? sol, CancellationToken ct)
        => _rest.PurchaseAsync(s, ctx, sol, ProviderName, callbackMode: true, ct);

    public Task<ValePedagioProviderOperationResult> SyncAsync(RestProviderSettings s, ValePedagioSolicitacao sol, CancellationToken ct)
        => _rest.SyncAsync(s, sol, ProviderName, ct);

    public Task<ValePedagioProviderOperationResult> CancelAsync(RestProviderSettings s, ValePedagioSolicitacao sol, CancellationToken ct)
        => _rest.CancelAsync(s, sol, ProviderName, ct);

    public Task<ValePedagioReceipt?> GetReceiptAsync(RestProviderSettings s, ValePedagioSolicitacao sol, CancellationToken ct)
        => _rest.GetReceiptAsync(s, sol, ProviderName, ct);
}
