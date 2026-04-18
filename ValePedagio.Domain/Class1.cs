using System.Text.Json.Serialization;

namespace ValePedagio.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValePedagioProviderType
{
    EFrete,
    DBTrans,
    Repom,
    DigitalCom,
    Ambipar,
    Extratta,
    Pamcard,
    QualP,
    SemParar,
    Target,
    NDDCargo
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValePedagioCapability
{
    Quote,
    Purchase,
    Cancel,
    Receipt,
    Callback,
    Retry
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValePedagioStatus
{
    EmProcessamento,
    Cotado,
    Comprado,
    Cancelado,
    Falha
}

public sealed record ValePedagioRoute(
    string UfOrigem,
    string UfDestino,
    IReadOnlyCollection<string> UfsPercurso,
    IReadOnlyCollection<string> PontosParada);

public sealed record ValePedagioRegulatoryItem(
    string CnpjFornecedor,
    string? DocumentoResponsavelPagamento,
    string NumeroCompra,
    decimal ValorValePedagio,
    string TipoValePedagio);

public sealed record ValePedagioReceipt(
    string FileName,
    string ContentType,
    byte[] Content,
    DateTimeOffset GeneratedAt);

public sealed record ValePedagioAuditTrail(
    string Operation,
    string? RequestPayload,
    string? ResponsePayload,
    DateTimeOffset OccurredAt);

public sealed record ValePedagioProviderDescriptor(
    ValePedagioProviderType Type,
    string DisplayName,
    int Wave,
    IReadOnlyCollection<ValePedagioCapability> Capabilities);

public sealed class ValePedagioProviderConfiguration
{
    private ValePedagioProviderConfiguration()
    {
    }

    public ValePedagioProviderConfiguration(
        string tenantId,
        ValePedagioProviderType provider,
        string displayName,
        int wave,
        bool enabled,
        string endpointBaseUrl,
        string callbackMode,
        Dictionary<string, string>? credentials = null)
    {
        TenantId = tenantId;
        Provider = provider;
        DisplayName = displayName;
        Wave = wave;
        Enabled = enabled;
        EndpointBaseUrl = endpointBaseUrl;
        CallbackMode = callbackMode;
        Credentials = credentials is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(credentials, StringComparer.OrdinalIgnoreCase);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public string TenantId { get; private set; } = string.Empty;
    public ValePedagioProviderType Provider { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public int Wave { get; private set; }
    public bool Enabled { get; set; } = true;
    public string EndpointBaseUrl { get; set; } = string.Empty;
    public string CallbackMode { get; set; } = "none";
    public Dictionary<string, string> Credentials { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ValePedagioSolicitacao
{
    private ValePedagioSolicitacao()
    {
        TenantId = string.Empty;
        TransportadorId = string.Empty;
        Route = new ValePedagioRoute(string.Empty, string.Empty, [], []);
    }

    public ValePedagioSolicitacao(
        Guid id,
        string tenantId,
        ValePedagioProviderType provider,
        string transportadorId,
        string? motoristaId,
        string? veiculoId,
        IEnumerable<string> cteIds,
        ValePedagioRoute route,
        decimal estimatedCargoValue,
        string? documentoResponsavelPagamento,
        string? observacoes,
        string? callbackUrl)
    {
        Id = id;
        TenantId = tenantId;
        Provider = provider;
        TransportadorId = transportadorId;
        MotoristaId = motoristaId;
        VeiculoId = veiculoId;
        CteIds = cteIds
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Route = route;
        EstimatedCargoValue = estimatedCargoValue;
        DocumentoResponsavelPagamento = documentoResponsavelPagamento;
        Observacoes = observacoes;
        CallbackUrl = callbackUrl;
        Status = ValePedagioStatus.EmProcessamento;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public string TenantId { get; private set; }
    public ValePedagioProviderType Provider { get; private set; }
    public string TransportadorId { get; private set; }
    public string? MotoristaId { get; private set; }
    public string? VeiculoId { get; private set; }
    public List<string> CteIds { get; private set; } = [];
    public ValePedagioRoute Route { get; private set; }
    public decimal EstimatedCargoValue { get; private set; }
    public string? DocumentoResponsavelPagamento { get; private set; }
    public string? Observacoes { get; private set; }
    public string? CallbackUrl { get; private set; }
    public ValePedagioStatus Status { get; private set; }
    public string? Protocolo { get; private set; }
    public string? NumeroCompra { get; private set; }
    public decimal? ValorTotal { get; private set; }
    public string? FailureReason { get; private set; }
    public int RetryCount { get; private set; }
    public string? RawRequestPayload { get; private set; }
    public string? RawResponsePayload { get; private set; }
    public ValePedagioReceipt? Receipt { get; private set; }
    public List<ValePedagioRegulatoryItem> RegulatoryItems { get; private set; } = [];
    public List<ValePedagioAuditTrail> AuditTrail { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void ApplyQuote(ValePedagioProviderOperationResult result, string? rawRequestPayload)
    {
        Status = ValePedagioStatus.Cotado;
        ApplyProviderResult(result, rawRequestPayload, "quote");
    }

    public void ApplyPurchase(ValePedagioProviderOperationResult result, string? rawRequestPayload)
    {
        Status = ValePedagioStatus.Comprado;
        ApplyProviderResult(result, rawRequestPayload, "purchase");
    }

    public void ApplyCancellation(ValePedagioProviderOperationResult result, string? rawRequestPayload)
    {
        Status = ValePedagioStatus.Cancelado;
        ApplyProviderResult(result, rawRequestPayload, "cancel");
    }

    public void ApplyFailure(string operation, string message, string? rawRequestPayload, string? rawResponsePayload)
    {
        Status = ValePedagioStatus.Falha;
        FailureReason = message;
        RawRequestPayload = rawRequestPayload;
        RawResponsePayload = rawResponsePayload;
        RetryCount += 1;
        UpdatedAt = DateTimeOffset.UtcNow;
        AuditTrail.Add(new ValePedagioAuditTrail(operation, rawRequestPayload, rawResponsePayload, UpdatedAt));
    }

    public void SetReceipt(ValePedagioReceipt receipt)
    {
        Receipt = receipt;
        UpdatedAt = DateTimeOffset.UtcNow;
        AuditTrail.Add(new ValePedagioAuditTrail("receipt", null, $"{{\"fileName\":\"{receipt.FileName}\"}}", UpdatedAt));
    }

    private void ApplyProviderResult(ValePedagioProviderOperationResult result, string? rawRequestPayload, string operation)
    {
        Protocolo = result.Protocolo;
        NumeroCompra = result.NumeroCompra;
        ValorTotal = result.ValorTotal;
        FailureReason = null;
        RawRequestPayload = rawRequestPayload;
        RawResponsePayload = result.RawResponsePayload;
        RegulatoryItems.Clear();
        RegulatoryItems.AddRange(result.ValesPedagio);
        if (result.Receipt is not null)
        {
            Receipt = result.Receipt;
        }

        UpdatedAt = DateTimeOffset.UtcNow;
        AuditTrail.Add(new ValePedagioAuditTrail(operation, rawRequestPayload, result.RawResponsePayload, UpdatedAt));
    }
}

public sealed record ValePedagioProviderOperationContext(
    string TenantId,
    string TransportadorId,
    string? MotoristaId,
    string? VeiculoId,
    IReadOnlyCollection<string> CteIds,
    ValePedagioRoute Route,
    decimal EstimatedCargoValue,
    string? DocumentoResponsavelPagamento,
    string? CallbackUrl,
    string? Observacoes);

public sealed record ValePedagioProviderOperationResult(
    string Protocolo,
    string NumeroCompra,
    decimal ValorTotal,
    IReadOnlyCollection<ValePedagioRegulatoryItem> ValesPedagio,
    ValePedagioReceipt? Receipt,
    string? RawResponsePayload);

public interface IValePedagioProvider
{
    ValePedagioProviderDescriptor Descriptor { get; }

    Task<ValePedagioProviderOperationResult> QuoteAsync(
        ValePedagioProviderOperationContext context,
        CancellationToken cancellationToken = default);

    Task<ValePedagioProviderOperationResult> PurchaseAsync(
        ValePedagioProviderOperationContext context,
        CancellationToken cancellationToken = default);

    Task<ValePedagioProviderOperationResult> CancelAsync(
        ValePedagioSolicitacao solicitacao,
        CancellationToken cancellationToken = default);

    Task<ValePedagioReceipt?> GetReceiptAsync(
        ValePedagioSolicitacao solicitacao,
        CancellationToken cancellationToken = default);
}

public interface IValePedagioProviderResolver
{
    Task<IValePedagioProvider> ResolveAsync(
        string tenantId,
        ValePedagioCapability requiredCapability,
        ValePedagioProviderType? preferredProvider,
        CancellationToken cancellationToken = default);

    IReadOnlyCollection<ValePedagioProviderDescriptor> GetCatalog();
}

public interface IValePedagioSolicitacaoRepository
{
    Task AddOrUpdateAsync(ValePedagioSolicitacao solicitacao, CancellationToken cancellationToken = default);
    Task<ValePedagioSolicitacao?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ValePedagioSolicitacao>> ListAsync(string tenantId, CancellationToken cancellationToken = default);
}

public interface IValePedagioProviderConfigurationRepository
{
    Task<ValePedagioProviderConfiguration> GetAsync(
        string tenantId,
        ValePedagioProviderType provider,
        CancellationToken cancellationToken = default);

    Task SaveAsync(ValePedagioProviderConfiguration configuration, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ValePedagioProviderConfiguration>> ListAsync(string tenantId, CancellationToken cancellationToken = default);
}
