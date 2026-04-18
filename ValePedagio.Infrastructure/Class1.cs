using System.Text;
using System.Text.Json;
using ValePedagio.Domain;

namespace ValePedagio.Infrastructure;

public static class ValePedagioProviderCatalog
{
    public static IReadOnlyCollection<ValePedagioProviderDescriptor> Descriptors { get; } =
    [
        new(ValePedagioProviderType.EFrete, "e-Frete", 1, [ValePedagioCapability.Quote, ValePedagioCapability.Purchase, ValePedagioCapability.Cancel, ValePedagioCapability.Receipt, ValePedagioCapability.Retry]),
        new(ValePedagioProviderType.DBTrans, "DBTrans", 1, [ValePedagioCapability.Quote, ValePedagioCapability.Purchase, ValePedagioCapability.Cancel, ValePedagioCapability.Receipt, ValePedagioCapability.Retry]),
        new(ValePedagioProviderType.Repom, "Repom", 1, [ValePedagioCapability.Quote, ValePedagioCapability.Purchase, ValePedagioCapability.Cancel, ValePedagioCapability.Receipt, ValePedagioCapability.Retry]),
        new(ValePedagioProviderType.DigitalCom, "DigitalCom", 1, [ValePedagioCapability.Quote, ValePedagioCapability.Purchase, ValePedagioCapability.Cancel, ValePedagioCapability.Receipt, ValePedagioCapability.Callback, ValePedagioCapability.Retry]),
        new(ValePedagioProviderType.Ambipar, "Ambipar", 2, [ValePedagioCapability.Quote, ValePedagioCapability.Purchase, ValePedagioCapability.Cancel, ValePedagioCapability.Receipt, ValePedagioCapability.Retry]),
        new(ValePedagioProviderType.Extratta, "Extratta", 2, [ValePedagioCapability.Quote, ValePedagioCapability.Purchase, ValePedagioCapability.Cancel, ValePedagioCapability.Receipt, ValePedagioCapability.Retry]),
        new(ValePedagioProviderType.Pamcard, "Pamcard", 2, [ValePedagioCapability.Quote, ValePedagioCapability.Purchase, ValePedagioCapability.Cancel, ValePedagioCapability.Receipt, ValePedagioCapability.Callback, ValePedagioCapability.Retry]),
        new(ValePedagioProviderType.QualP, "QualP", 3, [ValePedagioCapability.Quote, ValePedagioCapability.Purchase, ValePedagioCapability.Cancel, ValePedagioCapability.Receipt, ValePedagioCapability.Retry]),
        new(ValePedagioProviderType.SemParar, "SemParar", 3, [ValePedagioCapability.Quote, ValePedagioCapability.Purchase, ValePedagioCapability.Cancel, ValePedagioCapability.Receipt, ValePedagioCapability.Callback, ValePedagioCapability.Retry]),
        new(ValePedagioProviderType.Target, "Target", 3, [ValePedagioCapability.Quote, ValePedagioCapability.Purchase, ValePedagioCapability.Cancel, ValePedagioCapability.Receipt, ValePedagioCapability.Callback, ValePedagioCapability.Retry]),
        new(ValePedagioProviderType.NDDCargo, "NDDCargo", 3, [ValePedagioCapability.Quote, ValePedagioCapability.Purchase, ValePedagioCapability.Cancel, ValePedagioCapability.Receipt, ValePedagioCapability.Callback, ValePedagioCapability.Retry])
    ];
}

public static class ValePedagioProviderDocuments
{
    public static IReadOnlyDictionary<ValePedagioProviderType, string> Documents { get; } = new Dictionary<ValePedagioProviderType, string>
    {
        [ValePedagioProviderType.EFrete] = "11444777000101",
        [ValePedagioProviderType.DBTrans] = "22333888000102",
        [ValePedagioProviderType.Repom] = "33444999000103",
        [ValePedagioProviderType.DigitalCom] = "44555000000104",
        [ValePedagioProviderType.Ambipar] = "55666111000105",
        [ValePedagioProviderType.Extratta] = "66777222000106",
        [ValePedagioProviderType.Pamcard] = "77888333000107",
        [ValePedagioProviderType.QualP] = "88999444000108",
        [ValePedagioProviderType.SemParar] = "99000555000109",
        [ValePedagioProviderType.Target] = "10111666000110",
        [ValePedagioProviderType.NDDCargo] = "21222777000111"
    };
}

public static class ValePedagioProviderConfigurationFactory
{
    public static ValePedagioProviderConfiguration CreateDefault(string tenantId, ValePedagioProviderType provider)
    {
        var descriptor = ValePedagioProviderCatalog.Descriptors.Single(item => item.Type == provider);
        var slug = descriptor.DisplayName
            .ToLowerInvariant()
            .Replace(" ", "-", StringComparison.Ordinal)
            .Replace(".", "-", StringComparison.Ordinal)
            .Replace("á", "a", StringComparison.Ordinal)
            .Replace("ã", "a", StringComparison.Ordinal);

        if (provider == ValePedagioProviderType.EFrete)
        {
            return new ValePedagioProviderConfiguration(
                tenantId,
                provider,
                descriptor.DisplayName,
                descriptor.Wave,
                enabled: true,
                endpointBaseUrl: "https://dev.efrete.com.br/Services",
                callbackMode: "polling",
                credentials: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["integratorHash"] = string.Empty,
                    ["username"] = string.Empty,
                    ["password"] = string.Empty,
                    ["token"] = string.Empty,
                    ["providerDocument"] = ValePedagioProviderDocuments.Documents[provider],
                    ["documentType"] = "TAG",
                    ["timeoutSeconds"] = "30",
                    ["logonServicePath"] = "LogonService.asmx",
                    ["logonNamespace"] = "http://schemas.ipc.adm.br/efrete/logon",
                    ["logonOperation"] = "Login",
                    ["loginVersion"] = "1",
                    ["quoteServicePath"] = "ValePedagioService.asmx",
                    ["quoteOperation"] = "CalcularRota",
                    ["quoteAction"] = "http://schemas.ipc.adm.br/efrete/vale-pedagio/CalcularRota",
                    ["quoteVersion"] = "1",
                    ["purchaseServicePath"] = "ValePedagioService.asmx",
                    ["purchaseOperation"] = "ComprarValePedagio",
                    ["purchaseAction"] = "http://schemas.ipc.adm.br/efrete/vale-pedagio/ComprarValePedagio",
                    ["purchaseVersion"] = "1",
                    ["cancelServicePath"] = "ValePedagioService.asmx",
                    ["cancelOperation"] = "CancelarValePedagio",
                    ["cancelAction"] = "http://schemas.ipc.adm.br/efrete/vale-pedagio/CancelarValePedagio",
                    ["cancelVersion"] = "1",
                    ["receiptServicePath"] = "ValePedagioService.asmx",
                    ["receiptOperation"] = "ObterReciboValePedagio",
                    ["receiptAction"] = "http://schemas.ipc.adm.br/efrete/vale-pedagio/ObterReciboValePedagio",
                    ["receiptVersion"] = "1",
                    ["loginRequestTemplate"] = string.Empty,
                    ["quoteRequestTemplate"] = string.Empty,
                    ["purchaseRequestTemplate"] = string.Empty,
                    ["cancelRequestTemplate"] = string.Empty,
                    ["receiptRequestTemplate"] = string.Empty
                });
        }

        return new ValePedagioProviderConfiguration(
            tenantId,
            provider,
            descriptor.DisplayName,
            descriptor.Wave,
            enabled: true,
            endpointBaseUrl: $"https://sandbox.{slug}.vale-pedagio.local/v1",
            callbackMode: descriptor.Capabilities.Contains(ValePedagioCapability.Callback) ? "webhook" : "polling",
            credentials: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["clientId"] = $"{tenantId}-{slug}",
                ["apiKey"] = "sandbox-key",
                ["providerDocument"] = ValePedagioProviderDocuments.Documents[provider]
            });
    }
}

public sealed class ValePedagioProviderResolver : IValePedagioProviderResolver
{
    private readonly IReadOnlyDictionary<ValePedagioProviderType, IValePedagioProvider> _providers;
    private readonly IValePedagioProviderConfigurationRepository _configurationRepository;

    public ValePedagioProviderResolver(
        IEnumerable<IValePedagioProvider> providers,
        IValePedagioProviderConfigurationRepository configurationRepository)
    {
        _providers = providers.ToDictionary(item => item.Descriptor.Type);
        _configurationRepository = configurationRepository;
    }

    public IReadOnlyCollection<ValePedagioProviderDescriptor> GetCatalog()
    {
        return ValePedagioProviderCatalog.Descriptors;
    }

    public async Task<IValePedagioProvider> ResolveAsync(string tenantId, ValePedagioCapability requiredCapability, ValePedagioProviderType? preferredProvider, CancellationToken cancellationToken = default)
    {
        if (preferredProvider.HasValue)
        {
            var preferredDescriptor = ValePedagioProviderCatalog.Descriptors.SingleOrDefault(item => item.Type == preferredProvider.Value);
            if (preferredDescriptor is null)
            {
                throw new InvalidOperationException($"Provedor {preferredProvider.Value} não está implementado.");
            }

            if (!preferredDescriptor.Capabilities.Contains(requiredCapability))
            {
                throw new InvalidOperationException($"O provedor {preferredDescriptor.DisplayName} não suporta a operação {requiredCapability}.");
            }

            var preferredConfig = await _configurationRepository.GetAsync(tenantId, preferredProvider.Value, cancellationToken);
            if (!preferredConfig.Enabled)
            {
                throw new InvalidOperationException($"O provedor {preferredDescriptor.DisplayName} está desabilitado para o tenant {tenantId}.");
            }

            return _providers[preferredProvider.Value];
        }

        foreach (var descriptor in ValePedagioProviderCatalog.Descriptors.OrderBy(item => item.Wave).ThenBy(item => item.DisplayName))
        {
            if (!descriptor.Capabilities.Contains(requiredCapability))
            {
                continue;
            }

            var config = await _configurationRepository.GetAsync(tenantId, descriptor.Type, cancellationToken);
            if (config.Enabled)
            {
                return _providers[descriptor.Type];
            }
        }

        throw new InvalidOperationException($"Nenhum provedor habilitado suporta a operação {requiredCapability} para o tenant {tenantId}.");
    }
}

public sealed class CatalogValePedagioProvider : IValePedagioProvider
{
    public CatalogValePedagioProvider(ValePedagioProviderDescriptor descriptor)
    {
        Descriptor = descriptor;
    }

    public ValePedagioProviderDescriptor Descriptor { get; }

    public Task<ValePedagioProviderOperationResult> QuoteAsync(ValePedagioProviderOperationContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(BuildResult(context, purchase: false));
    }

    public Task<ValePedagioProviderOperationResult> PurchaseAsync(ValePedagioProviderOperationContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(BuildResult(context, purchase: true));
    }

    public Task<ValePedagioProviderOperationResult> CancelAsync(ValePedagioSolicitacao solicitacao, CancellationToken cancellationToken = default)
    {
        var rawResponse = JsonSerializer.Serialize(new
        {
            provider = Descriptor.DisplayName,
            operation = "cancel",
            solicitationId = solicitacao.Id,
            numeroCompra = solicitacao.NumeroCompra,
            status = "cancelled"
        });

        return Task.FromResult(new ValePedagioProviderOperationResult(
            $"{BuildSlug()}-CAN-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            solicitacao.NumeroCompra ?? $"{BuildSlug()}-CAN",
            solicitacao.ValorTotal ?? 0m,
            solicitacao.RegulatoryItems.ToList(),
            solicitacao.Receipt,
            rawResponse));
    }

    public Task<ValePedagioReceipt?> GetReceiptAsync(ValePedagioSolicitacao solicitacao, CancellationToken cancellationToken = default)
    {
        if (solicitacao.Receipt is not null)
        {
            return Task.FromResult<ValePedagioReceipt?>(solicitacao.Receipt);
        }

        if (solicitacao.NumeroCompra is null || solicitacao.ValorTotal is null)
        {
            return Task.FromResult<ValePedagioReceipt?>(null);
        }

        var receipt = BuildReceipt(
            new ValePedagioProviderOperationContext(
                solicitacao.TenantId,
                solicitacao.TransportadorId,
                solicitacao.MotoristaId,
                solicitacao.VeiculoId,
                solicitacao.CteIds,
                solicitacao.Route,
                solicitacao.EstimatedCargoValue,
                solicitacao.DocumentoResponsavelPagamento,
                solicitacao.CallbackUrl,
                solicitacao.Observacoes),
            solicitacao.Protocolo ?? $"{BuildSlug()}-REC-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            solicitacao.NumeroCompra,
            solicitacao.ValorTotal.Value,
            ResolveTipoValePedagio());

        return Task.FromResult<ValePedagioReceipt?>(receipt);
    }

    private ValePedagioProviderOperationResult BuildResult(ValePedagioProviderOperationContext context, bool purchase)
    {
        var amount = CalculateAmount(context);
        var protocol = $"{BuildSlug()}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        var numeroCompra = $"{(purchase ? "VP" : "COT")}-{BuildSlug()}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var tipoVale = ResolveTipoValePedagio();

        var vales = new List<ValePedagioRegulatoryItem>
        {
            new(
                ValePedagioProviderDocuments.Documents[Descriptor.Type],
                context.DocumentoResponsavelPagamento,
                numeroCompra,
                amount,
                tipoVale)
        };

        var receipt = purchase ? BuildReceipt(context, protocol, numeroCompra, amount, tipoVale) : null;

        var rawResponse = JsonSerializer.Serialize(new
        {
            provider = Descriptor.DisplayName,
            wave = Descriptor.Wave,
            status = purchase ? "purchased" : "quoted",
            protocol,
            numeroCompra,
            amount,
            route = new
            {
                context.Route.UfOrigem,
                context.Route.UfDestino,
                context.Route.UfsPercurso,
                context.Route.PontosParada
            },
            cteCount = context.CteIds.Count
        });

        return new ValePedagioProviderOperationResult(protocol, numeroCompra, amount, vales, receipt, rawResponse);
    }

    private ValePedagioReceipt BuildReceipt(ValePedagioProviderOperationContext context, string protocol, string numeroCompra, decimal amount, string tipoVale)
    {
        var lines = new[]
        {
            $"Recibo Vale-Pedágio - {Descriptor.DisplayName}",
            $"Protocolo: {protocol}",
            $"NumeroCompra: {numeroCompra}",
            $"TransportadorId: {context.TransportadorId}",
            $"MotoristaId: {context.MotoristaId}",
            $"VeiculoId: {context.VeiculoId}",
            $"UF Origem/Destino: {context.Route.UfOrigem} -> {context.Route.UfDestino}",
            $"UFs Percurso: {string.Join(", ", context.Route.UfsPercurso)}",
            $"Pontos de parada: {string.Join(", ", context.Route.PontosParada)}",
            $"CT-es: {string.Join(", ", context.CteIds)}",
            $"Tipo: {tipoVale}",
            $"Valor: {amount:F2}",
            $"GeradoEm: {DateTimeOffset.UtcNow:O}"
        };

        return new ValePedagioReceipt(
            $"vale-pedagio-{BuildSlug()}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.txt",
            "text/plain",
            Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines)),
            DateTimeOffset.UtcNow);
    }

    private decimal CalculateAmount(ValePedagioProviderOperationContext context)
    {
        var cteFactor = Math.Max(1, context.CteIds.Count) * 12m;
        var percursoFactor = Math.Max(1, context.Route.UfsPercurso.Count + 1) * 8.5m;
        var paradaFactor = Math.Max(1, context.Route.PontosParada.Count) * 6m;
        var cargaFactor = context.EstimatedCargoValue > 0 ? Math.Min(context.EstimatedCargoValue * 0.0015m, 280m) : 45m;
        var waveMultiplier = 1m + (Descriptor.Wave * 0.05m);
        return Math.Round((cteFactor + percursoFactor + paradaFactor + cargaFactor) * waveMultiplier, 2, MidpointRounding.AwayFromZero);
    }

    private string ResolveTipoValePedagio()
    {
        return Descriptor.Type switch
        {
            ValePedagioProviderType.SemParar or ValePedagioProviderType.EFrete => "TAG",
            ValePedagioProviderType.Repom or ValePedagioProviderType.DBTrans => "Cartao",
            _ => "Cupom"
        };
    }

    private string BuildSlug()
    {
        return Descriptor.DisplayName
            .ToUpperInvariant()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
    }
}
