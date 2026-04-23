using System.Text;
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
    Retry,
    Sync
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValePedagioStatus
{
    EmProcessamento,
    Cotado,
    Comprado,
    Confirmado,
    RotaSemCusto,
    AguardandoCadastroRota,
    Recusado,
    EmCancelamento,
    Cancelado,
    Encerrado,
    Falha
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValePedagioIntegrationMode
{
    Real,
    Simulated
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValePedagioFlowType
{
    QuoteOnly,
    QuoteAndPurchase
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValePedagioFailureCategory
{
    None,
    Validation,
    ProviderRejected,
    Integration,
    Timeout,
    OperationalPending
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValePedagioArtifactType
{
    Request,
    Response,
    Receipt,
    Callback,
    Sync,
    StatusSnapshot
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

public sealed record ValePedagioSyncAttempt(
    string Operation,
    bool Successful,
    string? RequestPayload,
    string? ResponsePayload,
    string? Message,
    DateTimeOffset OccurredAt);

public sealed record ValePedagioProviderArtifact(
    string Operation,
    ValePedagioArtifactType ArtifactType,
    string? FileName,
    string? ContentType,
    string Content,
    DateTimeOffset OccurredAt);

public sealed record ValePedagioProviderDescriptor(
    ValePedagioProviderType Type,
    string DisplayName,
    int Wave,
    IReadOnlyCollection<ValePedagioCapability> Capabilities,
    ValePedagioIntegrationMode IntegrationMode);

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
        ProviderStatus = "created";
    }

    public ValePedagioSolicitacao(
        Guid id,
        string tenantId,
        ValePedagioProviderType provider,
        ValePedagioIntegrationMode integrationMode,
        ValePedagioFlowType flowType,
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
        IntegrationMode = integrationMode;
        FlowType = flowType;
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
        ProviderStatus = "processing";
        FailureCategory = ValePedagioFailureCategory.None;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public string TenantId { get; private set; }
    public ValePedagioProviderType Provider { get; private set; }
    public ValePedagioIntegrationMode IntegrationMode { get; private set; }
    public ValePedagioFlowType FlowType { get; private set; }
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
    public string ProviderStatus { get; private set; } = "created";
    public string? Protocolo { get; private set; }
    public string? NumeroCompra { get; private set; }
    public decimal? ValorTotal { get; private set; }
    public string? FailureReason { get; private set; }
    public ValePedagioFailureCategory FailureCategory { get; private set; }
    public int RetryCount { get; private set; }
    public DateTimeOffset? LastSyncAt { get; private set; }
    public DateTimeOffset? NextRetryAt { get; private set; }
    public DateTimeOffset? ConcludedAt { get; private set; }
    public string? RawRequestPayload { get; private set; }
    public string? RawResponsePayload { get; private set; }
    public ValePedagioReceipt? Receipt { get; private set; }
    public List<ValePedagioRegulatoryItem> RegulatoryItems { get; private set; } = [];
    public List<ValePedagioAuditTrail> AuditTrail { get; private set; } = [];
    public List<ValePedagioSyncAttempt> SyncAttempts { get; private set; } = [];
    public List<ValePedagioProviderArtifact> ProviderArtifacts { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool CanPurchase => Status is ValePedagioStatus.Cotado or ValePedagioStatus.EmProcessamento;
    public bool CanCancel => Status is not ValePedagioStatus.Cancelado and not ValePedagioStatus.Encerrado and not ValePedagioStatus.Falha;
    public bool CanSync => Status is ValePedagioStatus.EmProcessamento or ValePedagioStatus.Comprado or ValePedagioStatus.Confirmado or ValePedagioStatus.EmCancelamento;
    public bool ArtifactsAvailable => ProviderArtifacts.Count > 0 || Receipt is not null;
    public bool IsImportableForMdfe => Status is ValePedagioStatus.Comprado or ValePedagioStatus.Confirmado;

    public void ApplyQuote(ValePedagioProviderOperationResult result, string? rawRequestPayload)
    {
        FlowType = ValePedagioFlowType.QuoteOnly;
        ApplyProviderResult(result, rawRequestPayload, "quote", ValePedagioStatus.Cotado);
    }

    public void ApplyPurchase(ValePedagioProviderOperationResult result, string? rawRequestPayload, bool fromExistingQuote = false)
    {
        FlowType = fromExistingQuote ? ValePedagioFlowType.QuoteAndPurchase : FlowType;
        ApplyProviderResult(result, rawRequestPayload, fromExistingQuote ? "purchase-from-quote" : "purchase", ValePedagioStatus.Comprado);
    }

    public void BeginCancellation()
    {
        Status = ValePedagioStatus.EmCancelamento;
        ProviderStatus = "cancellation_pending";
        FailureReason = null;
        FailureCategory = ValePedagioFailureCategory.None;
        UpdatedAt = DateTimeOffset.UtcNow;
        AuditTrail.Add(new ValePedagioAuditTrail("cancel-requested", null, null, UpdatedAt));
    }

    public void ApplyCancellation(ValePedagioProviderOperationResult result, string? rawRequestPayload)
    {
        ApplyProviderResult(result, rawRequestPayload, "cancel", ValePedagioStatus.Cancelado);
    }

    public void ApplySync(ValePedagioProviderOperationResult result, string? rawRequestPayload)
    {
        ApplyProviderResult(result, rawRequestPayload, "sync", Status);
        LastSyncAt = UpdatedAt;
    }

    public void ApplyCallback(ValePedagioProviderOperationResult result, string? rawRequestPayload)
    {
        ApplyProviderResult(result, rawRequestPayload, "callback", Status);
        LastSyncAt = UpdatedAt;
    }

    public void ApplyFailure(
        string operation,
        string message,
        string? rawRequestPayload,
        string? rawResponsePayload,
        ValePedagioFailureCategory category = ValePedagioFailureCategory.Integration,
        DateTimeOffset? nextRetryAt = null,
        bool keepCurrentStatus = false)
    {
        Status = keepCurrentStatus ? Status : ValePedagioStatus.Falha;
        ProviderStatus = keepCurrentStatus ? ProviderStatus : "failed";
        FailureReason = message;
        FailureCategory = category;
        RawRequestPayload = rawRequestPayload;
        RawResponsePayload = rawResponsePayload;
        RetryCount += 1;
        NextRetryAt = nextRetryAt;
        if (!keepCurrentStatus)
        {
            ConcludedAt = DateTimeOffset.UtcNow;
        }

        UpdatedAt = DateTimeOffset.UtcNow;
        AuditTrail.Add(new ValePedagioAuditTrail(operation, rawRequestPayload, rawResponsePayload, UpdatedAt));
        SyncAttempts.Add(new ValePedagioSyncAttempt(operation, Successful: false, rawRequestPayload, rawResponsePayload, message, UpdatedAt));
        AddArtifact(operation, ValePedagioArtifactType.Request, null, "application/json", rawRequestPayload);
        AddArtifact(operation, operation.Equals("callback", StringComparison.OrdinalIgnoreCase) ? ValePedagioArtifactType.Callback : ValePedagioArtifactType.Response, null, "application/json", rawResponsePayload);
    }

    public void SetReceipt(ValePedagioReceipt receipt)
    {
        Receipt = receipt;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddReceiptArtifact("receipt", receipt);
        AuditTrail.Add(new ValePedagioAuditTrail("receipt", null, $"{{\"fileName\":\"{receipt.FileName}\"}}", UpdatedAt));
    }

    private void ApplyProviderResult(ValePedagioProviderOperationResult result, string? rawRequestPayload, string operation, ValePedagioStatus fallbackStatus)
    {
        var resolvedStatus = result.SuggestedStatus ?? fallbackStatus;

        Status = resolvedStatus;
        ProviderStatus = string.IsNullOrWhiteSpace(result.ProviderStatus) ? ResolveDefaultProviderStatus(resolvedStatus) : result.ProviderStatus.Trim();
        Protocolo = string.IsNullOrWhiteSpace(result.Protocolo) ? Protocolo : result.Protocolo;
        NumeroCompra = string.IsNullOrWhiteSpace(result.NumeroCompra) ? NumeroCompra : result.NumeroCompra;
        ValorTotal = result.ValorTotal ?? ValorTotal;
        FailureReason = result.FailureReason;
        FailureCategory = result.FailureCategory ?? ValePedagioFailureCategory.None;
        RawRequestPayload = rawRequestPayload;
        RawResponsePayload = result.RawResponsePayload;
        NextRetryAt = result.NextRetryAt;

        RegulatoryItems.Clear();
        RegulatoryItems.AddRange(result.ValesPedagio);

        if (result.Receipt is not null)
        {
            Receipt = result.Receipt;
            AddReceiptArtifact(operation, result.Receipt);
        }

        UpdatedAt = DateTimeOffset.UtcNow;
        if (operation is "sync" or "callback")
        {
            LastSyncAt = UpdatedAt;
        }

        if (resolvedStatus is ValePedagioStatus.Cancelado or ValePedagioStatus.Encerrado or ValePedagioStatus.Confirmado or ValePedagioStatus.Recusado or ValePedagioStatus.RotaSemCusto or ValePedagioStatus.Falha)
        {
            ConcludedAt = UpdatedAt;
        }
        else if (operation is "sync" or "callback")
        {
            ConcludedAt = null;
        }

        AuditTrail.Add(new ValePedagioAuditTrail(operation, rawRequestPayload, result.RawResponsePayload, UpdatedAt));
        SyncAttempts.Add(new ValePedagioSyncAttempt(operation, Successful: true, rawRequestPayload, result.RawResponsePayload, null, UpdatedAt));
        AddArtifact(operation, operation.Equals("callback", StringComparison.OrdinalIgnoreCase) ? ValePedagioArtifactType.Callback : ValePedagioArtifactType.Request, null, "application/json", rawRequestPayload);
        AddArtifact(operation, operation is "sync" ? ValePedagioArtifactType.Sync : ValePedagioArtifactType.Response, null, "application/json", result.RawResponsePayload);
    }

    private void AddArtifact(string operation, ValePedagioArtifactType artifactType, string? fileName, string? contentType, string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        ProviderArtifacts.Add(new ValePedagioProviderArtifact(
            operation,
            artifactType,
            fileName,
            contentType,
            content,
            DateTimeOffset.UtcNow));
    }

    private void AddReceiptArtifact(string operation, ValePedagioReceipt receipt)
    {
        ProviderArtifacts.Add(new ValePedagioProviderArtifact(
            operation,
            ValePedagioArtifactType.Receipt,
            receipt.FileName,
            receipt.ContentType,
            Convert.ToBase64String(receipt.Content),
            receipt.GeneratedAt));
    }

    private static string ResolveDefaultProviderStatus(ValePedagioStatus status)
    {
        return status switch
        {
            ValePedagioStatus.EmProcessamento => "processing",
            ValePedagioStatus.Cotado => "quoted",
            ValePedagioStatus.Comprado => "purchased",
            ValePedagioStatus.Confirmado => "confirmed",
            ValePedagioStatus.RotaSemCusto => "zero_cost_route",
            ValePedagioStatus.AguardandoCadastroRota => "route_registration_pending",
            ValePedagioStatus.Recusado => "rejected",
            ValePedagioStatus.EmCancelamento => "cancellation_pending",
            ValePedagioStatus.Cancelado => "cancelled",
            ValePedagioStatus.Encerrado => "closed",
            ValePedagioStatus.Falha => "failed",
            _ => "unknown"
        };
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
    string? Protocolo,
    string? NumeroCompra,
    decimal? ValorTotal,
    IReadOnlyCollection<ValePedagioRegulatoryItem> ValesPedagio,
    ValePedagioReceipt? Receipt,
    string? RawResponsePayload,
    string? ProviderStatus = null,
    ValePedagioStatus? SuggestedStatus = null,
    string? FailureReason = null,
    ValePedagioFailureCategory? FailureCategory = null,
    DateTimeOffset? NextRetryAt = null);

public interface IValePedagioProvider
{
    ValePedagioProviderDescriptor Descriptor { get; }

    Task<ValePedagioProviderOperationResult> QuoteAsync(
        ValePedagioProviderOperationContext context,
        CancellationToken cancellationToken = default);

    Task<ValePedagioProviderOperationResult> PurchaseAsync(
        ValePedagioProviderOperationContext context,
        CancellationToken cancellationToken = default);

    Task<ValePedagioProviderOperationResult> PurchaseAsync(
        ValePedagioSolicitacao solicitacao,
        CancellationToken cancellationToken = default);

    Task<ValePedagioProviderOperationResult> SyncAsync(
        ValePedagioSolicitacao solicitacao,
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
    Task<IReadOnlyCollection<ValePedagioSolicitacao>> ListPendingSyncAsync(DateTimeOffset asOf, int maxItems, CancellationToken cancellationToken = default);
    Task<ValePedagioSolicitacao?> FindAsync(string tenantId, ValePedagioProviderType provider, Guid? id, string? numeroCompra, string? protocolo, CancellationToken cancellationToken = default);
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
