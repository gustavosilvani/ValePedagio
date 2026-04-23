using System.Globalization;
using System.Net;
using System.Security;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ValePedagio.Domain;

namespace ValePedagio.Infrastructure.Providers;

public sealed class EFreteValePedagioProvider : IValePedagioProvider
{
    private readonly IValePedagioProviderConfigurationRepository _configurationRepository;
    private readonly EFreteSoapClient _client;

    public EFreteValePedagioProvider(
        IValePedagioProviderConfigurationRepository configurationRepository,
        EFreteSoapClient client)
    {
        _configurationRepository = configurationRepository;
        _client = client;
    }

    public ValePedagioProviderDescriptor Descriptor =>
        ValePedagioProviderCatalog.Descriptors.Single(item => item.Type == ValePedagioProviderType.EFrete);

    public async Task<ValePedagioProviderOperationResult> QuoteAsync(ValePedagioProviderOperationContext context, CancellationToken cancellationToken = default)
    {
        var configuration = await _configurationRepository.GetAsync(context.TenantId, Descriptor.Type, cancellationToken);
        var settings = EFreteProviderSettings.FromConfiguration(configuration);
        return await _client.QuoteAsync(settings, context, cancellationToken);
    }

    public async Task<ValePedagioProviderOperationResult> PurchaseAsync(ValePedagioProviderOperationContext context, CancellationToken cancellationToken = default)
    {
        var configuration = await _configurationRepository.GetAsync(context.TenantId, Descriptor.Type, cancellationToken);
        var settings = EFreteProviderSettings.FromConfiguration(configuration);
        return await _client.PurchaseAsync(settings, context, cancellationToken);
    }

    public async Task<ValePedagioProviderOperationResult> PurchaseAsync(ValePedagioSolicitacao solicitacao, CancellationToken cancellationToken = default)
    {
        var configuration = await _configurationRepository.GetAsync(solicitacao.TenantId, Descriptor.Type, cancellationToken);
        var settings = EFreteProviderSettings.FromConfiguration(configuration);
        return await _client.PurchaseAsync(settings, solicitacao, cancellationToken);
    }

    public async Task<ValePedagioProviderOperationResult> SyncAsync(ValePedagioSolicitacao solicitacao, CancellationToken cancellationToken = default)
    {
        var configuration = await _configurationRepository.GetAsync(solicitacao.TenantId, Descriptor.Type, cancellationToken);
        var settings = EFreteProviderSettings.FromConfiguration(configuration);
        return await _client.SyncAsync(settings, solicitacao, cancellationToken);
    }

    public async Task<ValePedagioProviderOperationResult> CancelAsync(ValePedagioSolicitacao solicitacao, CancellationToken cancellationToken = default)
    {
        var configuration = await _configurationRepository.GetAsync(solicitacao.TenantId, Descriptor.Type, cancellationToken);
        var settings = EFreteProviderSettings.FromConfiguration(configuration);
        return await _client.CancelAsync(settings, solicitacao, cancellationToken);
    }

    public async Task<ValePedagioReceipt?> GetReceiptAsync(ValePedagioSolicitacao solicitacao, CancellationToken cancellationToken = default)
    {
        var configuration = await _configurationRepository.GetAsync(solicitacao.TenantId, Descriptor.Type, cancellationToken);
        var settings = EFreteProviderSettings.FromConfiguration(configuration);
        return await _client.GetReceiptAsync(settings, solicitacao, cancellationToken);
    }
}

public sealed class EFreteSoapClient
{
    private const string SoapContentType = "text/xml; charset=utf-8";
    private const string SoapEnvelopeNamespace = "http://schemas.xmlsoap.org/soap/envelope/";

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<EFreteSoapClient> _logger;

    public EFreteSoapClient(
        HttpClient httpClient,
        IMemoryCache memoryCache,
        ILogger<EFreteSoapClient> logger)
    {
        _httpClient = httpClient;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<ValePedagioProviderOperationResult> QuoteAsync(EFreteProviderSettings settings, ValePedagioProviderOperationContext context, CancellationToken cancellationToken)
    {
        var payload = await SendOperationAsync(
            settings,
            EFreteOperationKind.Quote,
            context.TenantId,
            BuildRequestBody(settings, EFreteOperationKind.Quote, context, null),
            cancellationToken);

        return BuildOperationResult(settings, context.DocumentoResponsavelPagamento, payload, defaultReceipt: null, "quoted", ValePedagioStatus.Cotado);
    }

    public async Task<ValePedagioProviderOperationResult> PurchaseAsync(EFreteProviderSettings settings, ValePedagioProviderOperationContext context, CancellationToken cancellationToken)
    {
        var payload = await SendOperationAsync(
            settings,
            EFreteOperationKind.Purchase,
            context.TenantId,
            BuildRequestBody(settings, EFreteOperationKind.Purchase, context, null),
            cancellationToken);

        var receipt = TryBuildReceipt(payload, settings);
        return BuildOperationResult(settings, context.DocumentoResponsavelPagamento, payload, receipt, "purchased", ValePedagioStatus.Comprado);
    }

    public async Task<ValePedagioProviderOperationResult> PurchaseAsync(EFreteProviderSettings settings, ValePedagioSolicitacao solicitacao, CancellationToken cancellationToken)
    {
        var payload = await SendOperationAsync(
            settings,
            EFreteOperationKind.Purchase,
            solicitacao.TenantId,
            BuildRequestBody(settings, EFreteOperationKind.Purchase, null, solicitacao),
            cancellationToken);

        var receipt = TryBuildReceipt(payload, settings) ?? solicitacao.Receipt;
        return BuildOperationResult(settings, solicitacao.DocumentoResponsavelPagamento, payload, receipt, "purchased", ValePedagioStatus.Comprado);
    }

    public async Task<ValePedagioProviderOperationResult> SyncAsync(EFreteProviderSettings settings, ValePedagioSolicitacao solicitacao, CancellationToken cancellationToken)
    {
        var payload = await SendOperationAsync(
            settings,
            EFreteOperationKind.Sync,
            solicitacao.TenantId,
            BuildRequestBody(settings, EFreteOperationKind.Sync, null, solicitacao),
            cancellationToken);

        var receipt = TryBuildReceipt(payload, settings) ?? solicitacao.Receipt;
        var providerStatus = TryResolveStatus(payload, solicitacao.Status);
        return BuildOperationResult(
            settings,
            solicitacao.DocumentoResponsavelPagamento,
            payload,
            receipt,
            providerStatus,
            MapStatus(providerStatus, solicitacao.Status));
    }

    public async Task<ValePedagioProviderOperationResult> CancelAsync(EFreteProviderSettings settings, ValePedagioSolicitacao solicitacao, CancellationToken cancellationToken)
    {
        var payload = await SendOperationAsync(
            settings,
            EFreteOperationKind.Cancel,
            solicitacao.TenantId,
            BuildRequestBody(settings, EFreteOperationKind.Cancel, null, solicitacao),
            cancellationToken);

        return new ValePedagioProviderOperationResult(
            payload.Protocol ?? solicitacao.Protocolo ?? "EFRETE-CANCEL",
            payload.PurchaseNumber ?? solicitacao.NumeroCompra ?? "EFRETE-CANCEL",
            payload.TotalAmount ?? solicitacao.ValorTotal ?? 0m,
            solicitacao.RegulatoryItems.ToList(),
            solicitacao.Receipt,
            payload.RawXml,
            "cancelled",
            ValePedagioStatus.Cancelado);
    }

    public async Task<ValePedagioReceipt?> GetReceiptAsync(EFreteProviderSettings settings, ValePedagioSolicitacao solicitacao, CancellationToken cancellationToken)
    {
        var payload = await SendOperationAsync(
            settings,
            EFreteOperationKind.Receipt,
            solicitacao.TenantId,
            BuildRequestBody(settings, EFreteOperationKind.Receipt, null, solicitacao),
            cancellationToken);

        return TryBuildReceipt(payload, settings) ?? solicitacao.Receipt;
    }

    private async Task<EFreteSoapPayload> SendOperationAsync(
        EFreteProviderSettings settings,
        EFreteOperationKind operation,
        string tenantId,
        string operationBody,
        CancellationToken cancellationToken)
    {
        var token = await AuthenticateAsync(settings, tenantId, cancellationToken);
        var requestBody = operationBody.Replace("{{Token}}", Escape(token), StringComparison.Ordinal);
        var operationConfig = settings.GetOperation(operation);
        var requestXml = BuildSoapEnvelope(requestBody);
        var url = BuildUrl(settings.EndpointBaseUrl, operationConfig.ServicePath);
        return await SendSoapAsync(url, operationConfig.SoapAction, requestXml, settings.Timeout, cancellationToken);
    }

    private async Task<string> AuthenticateAsync(EFreteProviderSettings settings, string tenantId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(settings.StaticToken))
        {
            return settings.StaticToken;
        }

        var cacheKey = $"efrete-token:{tenantId}:{settings.Username}:{settings.EndpointBaseUrl}";
        if (_memoryCache.TryGetValue(cacheKey, out string? cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
        {
            return cachedToken;
        }

        if (string.IsNullOrWhiteSpace(settings.Username) || string.IsNullOrWhiteSpace(settings.Password) || string.IsNullOrWhiteSpace(settings.IntegratorHash))
        {
            throw new InvalidOperationException("A configuração da e-Frete precisa de integratorHash e de username/password ou token fixo.");
        }

        var template = string.IsNullOrWhiteSpace(settings.LoginRequestTemplate)
            ? BuildDefaultLoginTemplate(settings)
            : settings.LoginRequestTemplate;

        var requestBody = ReplacePlaceholders(
            template,
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["IntegratorHash"] = settings.IntegratorHash,
                ["Username"] = settings.Username,
                ["Password"] = settings.Password,
                ["Version"] = settings.LoginVersion
            });

        var requestXml = BuildSoapEnvelope(requestBody);
        var url = BuildUrl(settings.EndpointBaseUrl, settings.LogonServicePath);
        var payload = await SendSoapAsync(url, settings.LogonAction, requestXml, settings.Timeout, cancellationToken);
        var token = payload.Token;

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("A autenticação da e-Frete retornou sucesso, mas sem token.");
        }

        _memoryCache.Set(cacheKey, token, TimeSpan.FromMinutes(20));
        return token;
    }

    private async Task<EFreteSoapPayload> SendSoapAsync(string url, string soapAction, string requestXml, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Provider"] = "EFrete",
            ["CorrelationId"] = correlationId
        });

        Exception? lastException = null;

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.TryAddWithoutValidation("SOAPAction", soapAction);
                request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
                request.Content = new StringContent(requestXml, Encoding.UTF8, "text/xml");
                request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(SoapContentType);

                _logger.LogInformation("Chamando e-Frete SOAP {Url} na tentativa {Attempt}.", url, attempt);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutCts.Token);
                var responseXml = await response.Content.ReadAsStringAsync(timeoutCts.Token);

                if ((int)response.StatusCode >= 500 && attempt < 2)
                {
                    _logger.LogWarning("e-Frete retornou {StatusCode}; nova tentativa curta sera executada.", response.StatusCode);
                    await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"e-Frete retornou HTTP {(int)response.StatusCode}: {TryExtractMessage(responseXml) ?? response.ReasonPhrase}");
                }

                return ParsePayload(responseXml);
            }
            catch (Exception ex) when (IsTransient(ex, cancellationToken) && attempt < 2)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Falha transitória no SOAP da e-Frete; repetindo tentativa.");
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                lastException = ex;
                break;
            }
        }

        throw lastException ?? new InvalidOperationException("Falha desconhecida ao chamar a e-Frete.");
    }

    private ValePedagioProviderOperationResult BuildOperationResult(
        EFreteProviderSettings settings,
        string? documentoResponsavelPagamento,
        EFreteSoapPayload payload,
        ValePedagioReceipt? defaultReceipt,
        string providerStatus,
        ValePedagioStatus suggestedStatus)
    {
        var numeroCompra = payload.PurchaseNumber ?? payload.Protocol ?? $"EFRETE-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var valorTotal = payload.TotalAmount ?? 0m;
        var tipoVale = string.IsNullOrWhiteSpace(settings.DocumentType) ? "TAG" : settings.DocumentType;

        var regulatoryItems = new List<ValePedagioRegulatoryItem>
        {
            new(
                settings.ProviderDocument,
                documentoResponsavelPagamento,
                numeroCompra,
                valorTotal,
                tipoVale)
        };

        return new ValePedagioProviderOperationResult(
            payload.Protocol ?? numeroCompra,
            numeroCompra,
            valorTotal,
            regulatoryItems,
            payload.Receipt ?? defaultReceipt,
            payload.RawXml,
            providerStatus,
            suggestedStatus);
    }

    private static string BuildRequestBody(
        EFreteProviderSettings settings,
        EFreteOperationKind operation,
        ValePedagioProviderOperationContext? context,
        ValePedagioSolicitacao? solicitacao)
    {
        var operationConfig = settings.GetOperation(operation);
        var template = operationConfig.RequestTemplate;
        if (string.IsNullOrWhiteSpace(template))
        {
            template = BuildDefaultOperationTemplate(operationConfig.OperationName, settings.OperationNamespace);
        }

        var estimatedCargoValue = context?.EstimatedCargoValue ?? solicitacao?.EstimatedCargoValue ?? 0m;

        var placeholders = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["IntegratorHash"] = settings.IntegratorHash,
            ["Token"] = "{{Token}}",
            ["Version"] = operationConfig.Version,
            ["TransportadorId"] = context?.TransportadorId ?? solicitacao?.TransportadorId,
            ["MotoristaId"] = context?.MotoristaId ?? solicitacao?.MotoristaId,
            ["VeiculoId"] = context?.VeiculoId ?? solicitacao?.VeiculoId,
            ["DocumentoResponsavelPagamento"] = context?.DocumentoResponsavelPagamento ?? solicitacao?.DocumentoResponsavelPagamento,
            ["EstimatedCargoValue"] = estimatedCargoValue.ToString("F2", CultureInfo.InvariantCulture),
            ["Observacoes"] = context?.Observacoes ?? solicitacao?.Observacoes,
            ["NumeroCompra"] = solicitacao?.NumeroCompra,
            ["Protocolo"] = solicitacao?.Protocolo,
            ["UfOrigem"] = context?.Route.UfOrigem ?? solicitacao?.Route.UfOrigem,
            ["UfDestino"] = context?.Route.UfDestino ?? solicitacao?.Route.UfDestino,
            ["UfsPercurso"] = BuildArrayXml("vp:string", context?.Route.UfsPercurso ?? solicitacao?.Route.UfsPercurso),
            ["PontosParada"] = BuildArrayXml("vp:string", context?.Route.PontosParada ?? solicitacao?.Route.PontosParada),
            ["CteIds"] = BuildArrayXml("vp:string", context?.CteIds ?? solicitacao?.CteIds),
            ["OperationName"] = operationConfig.OperationName,
            ["OperationNamespace"] = settings.OperationNamespace
        };

        return ReplacePlaceholders(template, placeholders);
    }

    private static string BuildDefaultLoginTemplate(EFreteProviderSettings settings)
    {
        return
            $"<log:{settings.LogonOperation}Request xmlns:log=\"{settings.LogonNamespace}\">{Environment.NewLine}" +
            "  <log:Integrador>{{IntegratorHash}}</log:Integrador>" + Environment.NewLine +
            "  <log:Usuario>{{Username}}</log:Usuario>" + Environment.NewLine +
            "  <log:Senha>{{Password}}</log:Senha>" + Environment.NewLine +
            "  <log:Versao>{{Version}}</log:Versao>" + Environment.NewLine +
            $"</log:{settings.LogonOperation}Request>";
    }

    private static string BuildDefaultOperationTemplate(string operationName, string operationNamespace)
    {
        return
            $"<vp:{operationName}Request xmlns:vp=\"{operationNamespace}\">{Environment.NewLine}" +
            "  <vp:Integrador>{{IntegratorHash}}</vp:Integrador>" + Environment.NewLine +
            "  <vp:Token>{{Token}}</vp:Token>" + Environment.NewLine +
            "  <vp:Versao>{{Version}}</vp:Versao>" + Environment.NewLine +
            "  <vp:TransportadorId>{{TransportadorId}}</vp:TransportadorId>" + Environment.NewLine +
            "  <vp:MotoristaId>{{MotoristaId}}</vp:MotoristaId>" + Environment.NewLine +
            "  <vp:VeiculoId>{{VeiculoId}}</vp:VeiculoId>" + Environment.NewLine +
            "  <vp:DocumentoResponsavelPagamento>{{DocumentoResponsavelPagamento}}</vp:DocumentoResponsavelPagamento>" + Environment.NewLine +
            "  <vp:NumeroCompra>{{NumeroCompra}}</vp:NumeroCompra>" + Environment.NewLine +
            "  <vp:Protocolo>{{Protocolo}}</vp:Protocolo>" + Environment.NewLine +
            "  <vp:ValorCarga>{{EstimatedCargoValue}}</vp:ValorCarga>" + Environment.NewLine +
            "  <vp:Observacoes>{{Observacoes}}</vp:Observacoes>" + Environment.NewLine +
            "  <vp:Rota>" + Environment.NewLine +
            "    <vp:UfOrigem>{{UfOrigem}}</vp:UfOrigem>" + Environment.NewLine +
            "    <vp:UfDestino>{{UfDestino}}</vp:UfDestino>" + Environment.NewLine +
            "    <vp:UfsPercurso>" + Environment.NewLine +
            "{{UfsPercurso}}" + Environment.NewLine +
            "    </vp:UfsPercurso>" + Environment.NewLine +
            "    <vp:PontosParada>" + Environment.NewLine +
            "{{PontosParada}}" + Environment.NewLine +
            "    </vp:PontosParada>" + Environment.NewLine +
            "  </vp:Rota>" + Environment.NewLine +
            "  <vp:CteIds>" + Environment.NewLine +
            "{{CteIds}}" + Environment.NewLine +
            "  </vp:CteIds>" + Environment.NewLine +
            $"</vp:{operationName}Request>";
    }

    private static string ReplacePlaceholders(string template, IReadOnlyDictionary<string, string?> placeholders)
    {
        var rawXmlPlaceholders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "UfsPercurso",
            "PontosParada",
            "CteIds"
        };

        var result = template;
        foreach (var pair in placeholders)
        {
            var replacement = rawXmlPlaceholders.Contains(pair.Key) ? pair.Value ?? string.Empty : Escape(pair.Value);
            result = result.Replace($"{{{{{pair.Key}}}}}", replacement, StringComparison.Ordinal);
        }

        return result;
    }

    private static string BuildSoapEnvelope(string body)
    {
        return $"""
<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
               xmlns:xsd="http://www.w3.org/2001/XMLSchema"
               xmlns:soap="{SoapEnvelopeNamespace}">
  <soap:Body>
{body}
  </soap:Body>
</soap:Envelope>
""";
    }

    private static string BuildArrayXml(string elementName, IEnumerable<string>? values)
    {
        if (values is null)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            values
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(value => $"      <{elementName}>{Escape(value)}</{elementName}>"));
    }

    private static EFreteSoapPayload ParsePayload(string responseXml)
    {
        var document = XDocument.Parse(responseXml, LoadOptions.PreserveWhitespace);
        var fault = document.Descendants().FirstOrDefault(static item => item.Name.LocalName.Equals("Fault", StringComparison.OrdinalIgnoreCase));
        if (fault is not null)
        {
            throw new InvalidOperationException(TryExtractMessage(fault.ToString()) ?? "SOAP fault retornado pela e-Frete.");
        }

        var success = TryGetBool(document, "Sucesso");
        if (success == false)
        {
            throw new InvalidOperationException(TryExtractMessage(responseXml) ?? "A e-Frete rejeitou a operação.");
        }

        return new EFreteSoapPayload(
            responseXml,
            TryGetValue(document, "Token"),
            TryGetValue(document, "Protocolo") ?? TryGetValue(document, "ProtocoloServico"),
            TryGetValue(document, "NumeroCompra") ?? TryGetValue(document, "CodigoCompra") ?? TryGetValue(document, "NumeroOperacao"),
            TryGetDecimal(document, "ValorTotal") ?? TryGetDecimal(document, "Valor") ?? TryGetDecimal(document, "TotalPedagio"),
            TryParseReceipt(document));
    }

    private static string TryResolveStatus(EFreteSoapPayload payload, ValePedagioStatus fallbackStatus)
    {
        var raw = payload.RawXml;
        if (raw.Contains("Confirmado", StringComparison.OrdinalIgnoreCase) || raw.Contains("confirmed", StringComparison.OrdinalIgnoreCase))
        {
            return "confirmed";
        }

        if (raw.Contains("Cancelado", StringComparison.OrdinalIgnoreCase) || raw.Contains("cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return "cancelled";
        }

        if (raw.Contains("Cotado", StringComparison.OrdinalIgnoreCase) || raw.Contains("quoted", StringComparison.OrdinalIgnoreCase))
        {
            return "quoted";
        }

        return fallbackStatus switch
        {
            ValePedagioStatus.Comprado => "purchased",
            ValePedagioStatus.Confirmado => "confirmed",
            ValePedagioStatus.EmCancelamento => "cancellation_pending",
            _ => "synced"
        };
    }

    private static ValePedagioStatus MapStatus(string providerStatus, ValePedagioStatus fallbackStatus)
    {
        return providerStatus switch
        {
            "quoted" => ValePedagioStatus.Cotado,
            "purchased" => ValePedagioStatus.Comprado,
            "confirmed" => ValePedagioStatus.Confirmado,
            "cancelled" => ValePedagioStatus.Cancelado,
            _ => fallbackStatus
        };
    }

    private static ValePedagioReceipt? TryBuildReceipt(EFreteSoapPayload payload, EFreteProviderSettings settings)
    {
        return payload.Receipt ?? TryBuildFallbackReceipt(payload, settings);
    }

    private static ValePedagioReceipt? TryBuildFallbackReceipt(EFreteSoapPayload payload, EFreteProviderSettings settings)
    {
        if (string.IsNullOrWhiteSpace(payload.PurchaseNumber) || payload.TotalAmount is null)
        {
            return null;
        }

        var lines = new[]
        {
            "Recibo Vale-Pedágio - e-Frete",
            $"NumeroCompra: {payload.PurchaseNumber}",
            $"Protocolo: {payload.Protocol}",
            $"Valor: {payload.TotalAmount.Value.ToString("F2", CultureInfo.InvariantCulture)}",
            $"Tipo: {settings.DocumentType}"
        };

        return new ValePedagioReceipt(
            $"efrete-recibo-{payload.PurchaseNumber}.txt",
            "text/plain",
            Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines)),
            DateTimeOffset.UtcNow);
    }

    private static ValePedagioReceipt? TryParseReceipt(XDocument document)
    {
        var base64 = TryGetValue(document, "Arquivo") ?? TryGetValue(document, "Pdf") ?? TryGetValue(document, "Recibo");
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        try
        {
            var fileName = TryGetValue(document, "Filename") ?? TryGetValue(document, "NomeArquivo") ?? $"efrete-recibo-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.pdf";
            var contentType = TryGetValue(document, "Mimetype") ?? TryGetValue(document, "MimeType") ?? "application/pdf";
            return new ValePedagioReceipt(
                fileName,
                contentType,
                Convert.FromBase64String(base64),
                DateTimeOffset.UtcNow);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static bool IsTransient(Exception exception, CancellationToken cancellationToken)
    {
        return exception switch
        {
            HttpRequestException => true,
            TaskCanceledException when !cancellationToken.IsCancellationRequested => true,
            _ => false
        };
    }

    private static string BuildUrl(string baseUrl, string relativePath)
    {
        return $"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }

    private static string Escape(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : SecurityElement.Escape(value) ?? string.Empty;
    }

    private static bool? TryGetBool(XContainer container, string localName)
    {
        var value = TryGetValue(container, localName);
        return bool.TryParse(value, out var parsed) ? parsed : null;
    }

    private static decimal? TryGetDecimal(XContainer container, string localName)
    {
        var value = TryGetValue(container, localName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var invariant))
        {
            return invariant;
        }

        return decimal.TryParse(value, NumberStyles.Any, new CultureInfo("pt-BR"), out var brazilian) ? brazilian : null;
    }

    private static string? TryExtractMessage(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml);
            return TryGetValue(document, "Mensagem")
                ?? TryGetValue(document, "faultstring")
                ?? TryGetValue(document, "Text");
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetValue(XContainer container, string localName)
    {
        return container
            .Descendants()
            .FirstOrDefault(item => item.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private sealed record EFreteSoapPayload(
        string RawXml,
        string? Token,
        string? Protocol,
        string? PurchaseNumber,
        decimal? TotalAmount,
        ValePedagioReceipt? Receipt);
}

public sealed record EFreteProviderSettings(
    string EndpointBaseUrl,
    string IntegratorHash,
    string? Username,
    string? Password,
    string? StaticToken,
    string ProviderDocument,
    string DocumentType,
    string LogonNamespace,
    string LogonOperation,
    string LogonAction,
    string LogonServicePath,
    string LoginVersion,
    string OperationNamespace,
    string? LoginRequestTemplate,
    EFreteOperationConfiguration Quote,
    EFreteOperationConfiguration Purchase,
    EFreteOperationConfiguration Sync,
    EFreteOperationConfiguration Cancel,
    EFreteOperationConfiguration Receipt,
    TimeSpan Timeout)
{
    public EFreteOperationConfiguration GetOperation(EFreteOperationKind kind)
    {
        return kind switch
        {
            EFreteOperationKind.Quote => Quote,
            EFreteOperationKind.Purchase => Purchase,
            EFreteOperationKind.Sync => Sync,
            EFreteOperationKind.Cancel => Cancel,
            EFreteOperationKind.Receipt => Receipt,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public static EFreteProviderSettings FromConfiguration(ValePedagioProviderConfiguration configuration)
    {
        static string? TryGet(IReadOnlyDictionary<string, string> values, string key)
            => values.TryGetValue(key, out var value) ? value : null;

        var credentials = configuration.Credentials;
        var timeoutSeconds = int.TryParse(TryGet(credentials, "timeoutSeconds"), out var parsedTimeout) ? parsedTimeout : 30;
        var operationNamespace = TryGet(credentials, "operationNamespace") ?? "http://schemas.ipc.adm.br/efrete/vale-pedagio";
        var logonNamespace = TryGet(credentials, "logonNamespace") ?? "http://schemas.ipc.adm.br/efrete/logon";
        var logonOperation = TryGet(credentials, "logonOperation") ?? "Login";

        return new EFreteProviderSettings(
            configuration.EndpointBaseUrl,
            TryGet(credentials, "integratorHash") ?? string.Empty,
            TryGet(credentials, "username"),
            TryGet(credentials, "password"),
            TryGet(credentials, "token"),
            TryGet(credentials, "providerDocument") ?? ValePedagioProviderDocuments.Documents[ValePedagioProviderType.EFrete],
            TryGet(credentials, "documentType") ?? "TAG",
            logonNamespace,
            logonOperation,
            TryGet(credentials, "logonAction") ?? $"{logonNamespace.TrimEnd('/')}/{logonOperation}",
            TryGet(credentials, "logonServicePath") ?? "LogonService.asmx",
            TryGet(credentials, "loginVersion") ?? "1",
            operationNamespace,
            TryGet(credentials, "loginRequestTemplate"),
            new EFreteOperationConfiguration(
                TryGet(credentials, "quoteServicePath") ?? "ValePedagioService.asmx",
                TryGet(credentials, "quoteOperation") ?? "CalcularRota",
                TryGet(credentials, "quoteAction") ?? $"{operationNamespace.TrimEnd('/')}/CalcularRota",
                TryGet(credentials, "quoteVersion") ?? "1",
                TryGet(credentials, "quoteRequestTemplate")),
            new EFreteOperationConfiguration(
                TryGet(credentials, "purchaseServicePath") ?? "ValePedagioService.asmx",
                TryGet(credentials, "purchaseOperation") ?? "ComprarValePedagio",
                TryGet(credentials, "purchaseAction") ?? $"{operationNamespace.TrimEnd('/')}/ComprarValePedagio",
                TryGet(credentials, "purchaseVersion") ?? "1",
                TryGet(credentials, "purchaseRequestTemplate")),
            new EFreteOperationConfiguration(
                TryGet(credentials, "syncServicePath") ?? "ValePedagioService.asmx",
                TryGet(credentials, "syncOperation") ?? "ConsultarValePedagio",
                TryGet(credentials, "syncAction") ?? $"{operationNamespace.TrimEnd('/')}/ConsultarValePedagio",
                TryGet(credentials, "syncVersion") ?? "1",
                TryGet(credentials, "syncRequestTemplate")),
            new EFreteOperationConfiguration(
                TryGet(credentials, "cancelServicePath") ?? "ValePedagioService.asmx",
                TryGet(credentials, "cancelOperation") ?? "CancelarValePedagio",
                TryGet(credentials, "cancelAction") ?? $"{operationNamespace.TrimEnd('/')}/CancelarValePedagio",
                TryGet(credentials, "cancelVersion") ?? "1",
                TryGet(credentials, "cancelRequestTemplate")),
            new EFreteOperationConfiguration(
                TryGet(credentials, "receiptServicePath") ?? "ValePedagioService.asmx",
                TryGet(credentials, "receiptOperation") ?? "ObterReciboValePedagio",
                TryGet(credentials, "receiptAction") ?? $"{operationNamespace.TrimEnd('/')}/ObterReciboValePedagio",
                TryGet(credentials, "receiptVersion") ?? "1",
                TryGet(credentials, "receiptRequestTemplate")),
            TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 120)));
    }
}

public sealed record EFreteOperationConfiguration(
    string ServicePath,
    string OperationName,
    string SoapAction,
    string Version,
    string? RequestTemplate);

public enum EFreteOperationKind
{
    Quote,
    Purchase,
    Sync,
    Cancel,
    Receipt
}
