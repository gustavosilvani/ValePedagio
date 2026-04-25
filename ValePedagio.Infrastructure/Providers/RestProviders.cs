// Shared REST infrastructure for Wave 1-3 vale-pedágio providers.
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ValePedagio.Domain;

namespace ValePedagio.Infrastructure.Providers;

internal sealed record RestProviderSettings(
    string EndpointBaseUrl,
    string ClientId,
    string ApiKey,
    string ProviderDocument,
    string DocumentType,
    TimeSpan Timeout,
    bool RetryEnabled = false)
{
    internal static string? G(IReadOnlyDictionary<string, string> c, string k) =>
        c.TryGetValue(k, out var v) ? v : null;
}

internal sealed class RestProviderResponse
{
    [JsonPropertyName("protocolo")] public string? Protocolo { get; set; }
    [JsonPropertyName("protocol")] public string? Protocol { get; set; }
    [JsonPropertyName("numeroCompra")] public string? NumeroCompra { get; set; }
    [JsonPropertyName("purchaseNumber")] public string? PurchaseNumber { get; set; }
    [JsonPropertyName("valorTotal")] public decimal? ValorTotal { get; set; }
    [JsonPropertyName("totalAmount")] public decimal? TotalAmount { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("motivoRejeicao")] public string? MotivoRejeicao { get; set; }
    [JsonPropertyName("failureReason")] public string? FailureReason { get; set; }
    [JsonPropertyName("reciboBase64")] public string? ReciboBase64 { get; set; }
    [JsonPropertyName("receiptBase64")] public string? ReceiptBase64 { get; set; }
    [JsonPropertyName("reciboFileName")] public string? ReciboFileName { get; set; }
    [JsonPropertyName("receiptFileName")] public string? ReceiptFileName { get; set; }
    [JsonPropertyName("reciboContentType")] public string? ReciboContentType { get; set; }
    [JsonPropertyName("receiptContentType")] public string? ReceiptContentType { get; set; }

    public string? ResolvedProtocolo => Protocolo ?? Protocol;
    public string? ResolvedNumeroCompra => NumeroCompra ?? PurchaseNumber;
    public decimal? ResolvedValorTotal => ValorTotal ?? TotalAmount;
    public string? ResolvedReciboBase64 => ReciboBase64 ?? ReceiptBase64;
    public string? ResolvedReciboFileName => ReciboFileName ?? ReceiptFileName;
    public string? ResolvedReciboContentType => ReciboContentType ?? ReceiptContentType;
    public string? ResolvedMotivoRejeicao => MotivoRejeicao ?? FailureReason;
}

internal sealed class RestValePedagioHttpClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly ILogger _logger;

    public RestValePedagioHttpClient(HttpClient http, ILogger logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<ValePedagioProviderOperationResult> QuoteAsync(
        RestProviderSettings s, ValePedagioProviderOperationContext ctx, string name, CancellationToken ct)
    {
        var r = await CallAsync(s, "POST", $"{s.EndpointBaseUrl}/v1/cotacao", BuildBody(ctx, null), name, ct);
        return BuildResult(s, r, ctx.DocumentoResponsavelPagamento, ValePedagioStatus.Cotado, "quoted");
    }

    public async Task<ValePedagioProviderOperationResult> PurchaseAsync(
        RestProviderSettings s, ValePedagioProviderOperationContext? ctx, ValePedagioSolicitacao? sol, string name, bool callbackMode, CancellationToken ct)
    {
        var doc = ctx?.DocumentoResponsavelPagamento ?? sol?.DocumentoResponsavelPagamento;
        var r = await CallAsync(s, "POST", $"{s.EndpointBaseUrl}/v1/compra", BuildBody(ctx, sol), name, ct);
        var status = callbackMode ? ValePedagioStatus.EmProcessamento : ValePedagioStatus.Comprado;
        var pStatus = callbackMode ? "processing" : "purchased";
        return BuildResult(s, r, doc, status, pStatus);
    }

    public async Task<ValePedagioProviderOperationResult> SyncAsync(
        RestProviderSettings s, ValePedagioSolicitacao sol, string name, CancellationToken ct)
    {
        var r = await CallAsync(s, "GET", $"{s.EndpointBaseUrl}/v1/consulta/{sol.NumeroCompra}", null, name, ct);
        return BuildResult(s, r, sol.DocumentoResponsavelPagamento, sol.Status, sol.ProviderStatus);
    }

    public async Task<ValePedagioProviderOperationResult> CancelAsync(
        RestProviderSettings s, ValePedagioSolicitacao sol, string name, CancellationToken ct)
    {
        var r = await CallAsync(s, "POST", $"{s.EndpointBaseUrl}/v1/cancelar/{sol.NumeroCompra}", BuildBody(null, sol), name, ct);
        return BuildResult(s, r, sol.DocumentoResponsavelPagamento, ValePedagioStatus.Cancelado, "cancelled");
    }

    public async Task<ValePedagioReceipt?> GetReceiptAsync(
        RestProviderSettings s, ValePedagioSolicitacao sol, string name, CancellationToken ct)
    {
        if (sol.NumeroCompra is null) return null;
        try
        {
            var r = await CallAsync(s, "GET", $"{s.EndpointBaseUrl}/v1/recibo/{sol.NumeroCompra}", null, name, ct);
            return TryBuildReceipt(r, name.ToLowerInvariant());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao obter recibo {Provider} para {NumeroCompra}", name, sol.NumeroCompra);
            return sol.Receipt;
        }
    }

    private async Task<RestProviderResponse> CallAsync(
        RestProviderSettings s, string method, string url, object? body, string name, CancellationToken ct)
    {
        var jsonBody = body is null ? null : JsonSerializer.Serialize(body);
        var maxAttempts = s.RetryEnabled ? 2 : 1;
        Exception? lastEx = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(s.Timeout);
            try
            {
                using var req = BuildRequest(s, method, url, jsonBody);
                _logger.LogInformation("Chamando {Provider} {Method} {Url} tentativa {Attempt}", name, method, url, attempt);
                using var resp = await _http.SendAsync(req, cts.Token);
                var json = await resp.Content.ReadAsStringAsync(cts.Token);

                if ((int)resp.StatusCode >= 500 && attempt < maxAttempts)
                {
                    _logger.LogWarning("{Provider} retornou {Status}; nova tentativa.", name, (int)resp.StatusCode);
                    await Task.Delay(300, ct);
                    continue;
                }

                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException($"{name} retornou HTTP {(int)resp.StatusCode}: {TryExtractError(json)}");

                return JsonSerializer.Deserialize<RestProviderResponse>(json, JsonOpts) ?? new RestProviderResponse();
            }
            catch (Exception ex) when (IsTransient(ex, ct) && attempt < maxAttempts)
            {
                lastEx = ex;
                _logger.LogWarning(ex, "Falha transitória {Provider}; repetindo.", name);
                await Task.Delay(300, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                throw new InvalidOperationException($"Falha ao chamar {name}: {ex.Message}", ex);
            }
        }

        throw lastEx ?? new InvalidOperationException($"Falha desconhecida ao chamar {name}.");
    }

    private static HttpRequestMessage BuildRequest(RestProviderSettings s, string method, string url, string? jsonBody)
    {
        var req = new HttpRequestMessage(new HttpMethod(method), url);
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{s.ClientId}:{s.ApiKey}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.TryAddWithoutValidation("Accept", "application/json");
        if (jsonBody is not null)
            req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        return req;
    }

    private static ValePedagioProviderOperationResult BuildResult(
        RestProviderSettings s, RestProviderResponse r, string? doc, ValePedagioStatus fallbackStatus, string fallbackProviderStatus)
    {
        var numCompra = r.ResolvedNumeroCompra ?? $"REST-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var valor = r.ResolvedValorTotal ?? 0m;
        var status = MapStatus(r.Status, fallbackStatus);

        return new ValePedagioProviderOperationResult(
            r.ResolvedProtocolo ?? numCompra,
            numCompra,
            valor,
            [new(s.ProviderDocument, doc, numCompra, valor, s.DocumentType)],
            TryBuildReceipt(r, "rest"),
            JsonSerializer.Serialize(r),
            r.Status ?? fallbackProviderStatus,
            status,
            r.ResolvedMotivoRejeicao,
            string.IsNullOrWhiteSpace(r.ResolvedMotivoRejeicao) ? null : ValePedagioFailureCategory.ProviderRejected);
    }

    internal static ValePedagioStatus MapStatus(string? s, ValePedagioStatus fallback) => s?.Trim().ToLowerInvariant() switch
    {
        "quoted" or "cotado" => ValePedagioStatus.Cotado,
        "purchased" or "comprado" => ValePedagioStatus.Comprado,
        "confirmed" or "confirmado" => ValePedagioStatus.Confirmado,
        "processing" or "em_processamento" => ValePedagioStatus.EmProcessamento,
        "route_without_cost" or "rota_sem_custo" or "zero_cost" => ValePedagioStatus.RotaSemCusto,
        "route_registration_pending" or "aguardando_cadastro" => ValePedagioStatus.AguardandoCadastroRota,
        "rejected" or "recusado" => ValePedagioStatus.Recusado,
        "cancellation_pending" or "em_cancelamento" => ValePedagioStatus.EmCancelamento,
        "cancelled" or "cancelado" => ValePedagioStatus.Cancelado,
        "failed" or "falha" => ValePedagioStatus.Falha,
        _ => fallback
    };

    internal static ValePedagioReceipt? TryBuildReceipt(RestProviderResponse r, string slug)
    {
        if (string.IsNullOrWhiteSpace(r.ResolvedReciboBase64)) return null;
        try
        {
            return new ValePedagioReceipt(
                r.ResolvedReciboFileName ?? $"{slug}-recibo-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.pdf",
                r.ResolvedReciboContentType ?? "application/pdf",
                Convert.FromBase64String(r.ResolvedReciboBase64),
                DateTimeOffset.UtcNow);
        }
        catch (FormatException) { return null; }
    }

    internal static object BuildBody(ValePedagioProviderOperationContext? ctx, ValePedagioSolicitacao? sol) => new
    {
        transportadorId = ctx?.TransportadorId ?? sol?.TransportadorId,
        motoristaId = ctx?.MotoristaId ?? sol?.MotoristaId,
        veiculoId = ctx?.VeiculoId ?? sol?.VeiculoId,
        cteIds = (IEnumerable<string>?)ctx?.CteIds ?? sol?.CteIds,
        ufOrigem = ctx?.Route.UfOrigem ?? sol?.Route.UfOrigem,
        ufDestino = ctx?.Route.UfDestino ?? sol?.Route.UfDestino,
        ufsPercurso = (IEnumerable<string>?)ctx?.Route.UfsPercurso ?? sol?.Route.UfsPercurso,
        valorCarga = ctx?.EstimatedCargoValue ?? sol?.EstimatedCargoValue ?? 0m,
        documentoResponsavelPagamento = ctx?.DocumentoResponsavelPagamento ?? sol?.DocumentoResponsavelPagamento,
        observacoes = ctx?.Observacoes ?? sol?.Observacoes,
        numeroCompra = sol?.NumeroCompra,
        protocolo = sol?.Protocolo
    };

    private static bool IsTransient(Exception ex, CancellationToken ct) => ex switch
    {
        HttpRequestException => true,
        TaskCanceledException when !ct.IsCancellationRequested => true,
        _ => false
    };

    private static string TryExtractError(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("message", out var m)) return m.GetString() ?? json;
            if (doc.RootElement.TryGetProperty("error", out var e)) return e.GetString() ?? json;
        }
        catch { }
        return json.Length > 200 ? json[..200] : json;
    }
}
