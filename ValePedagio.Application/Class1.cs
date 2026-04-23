using System.Text.Json;
using ValePedagio.Domain;

namespace ValePedagio.Application;

public sealed record ValePedagioRouteDto(
    string UfOrigem,
    string UfDestino,
    IReadOnlyCollection<string> UfsPercurso,
    IReadOnlyCollection<string> PontosParada);

public sealed record ValePedagioRegulatoryItemDto(
    string CnpjFornecedor,
    string? DocumentoResponsavelPagamento,
    string NumeroCompra,
    decimal ValorValePedagio,
    string TipoValePedagio);

public sealed record ValePedagioAuditTrailDto(
    string Operation,
    string? RequestPayload,
    string? ResponsePayload,
    DateTimeOffset OccurredAt);

public sealed record ValePedagioSyncAttemptDto(
    string Operation,
    bool Successful,
    string? RequestPayload,
    string? ResponsePayload,
    string? Message,
    DateTimeOffset OccurredAt);

public sealed record ValePedagioProviderArtifactDto(
    string Operation,
    ValePedagioArtifactType ArtifactType,
    string? FileName,
    string? ContentType,
    DateTimeOffset OccurredAt);

public sealed record ValePedagioProviderSummaryDto(
    ValePedagioProviderType Type,
    string DisplayName,
    int Wave,
    IReadOnlyCollection<ValePedagioCapability> Capabilities,
    bool Enabled,
    string EndpointBaseUrl,
    string CallbackMode,
    ValePedagioIntegrationMode IntegrationMode);

public sealed record ValePedagioProviderConfigurationDto(
    string TenantId,
    ValePedagioProviderType Provider,
    string DisplayName,
    int Wave,
    IReadOnlyCollection<ValePedagioCapability> Capabilities,
    bool Enabled,
    string EndpointBaseUrl,
    string CallbackMode,
    IReadOnlyDictionary<string, string> Credentials,
    DateTimeOffset UpdatedAt,
    ValePedagioIntegrationMode IntegrationMode);

public sealed record ValePedagioProviderConfigurationRequest(
    bool Enabled,
    string? EndpointBaseUrl,
    string? CallbackMode,
    Dictionary<string, string>? Credentials);

public sealed record ValePedagioSolicitacaoRequest(
    string TransportadorId,
    string? MotoristaId,
    string? VeiculoId,
    IReadOnlyCollection<string> CteIds,
    ValePedagioRouteDto Route,
    decimal EstimatedCargoValue,
    ValePedagioProviderType? PreferredProvider,
    string? DocumentoResponsavelPagamento,
    string? Observacoes,
    string? CallbackUrl);

public sealed record ValePedagioProviderCallbackRequest(
    Guid? SolicitacaoId,
    string? NumeroCompra,
    string? Protocolo,
    string? ProviderStatus,
    decimal? ValorTotal,
    string? FailureReason,
    string? ReceiptBase64,
    string? ReceiptFileName,
    string? ReceiptContentType,
    string? RawPayload);

public sealed record ValePedagioSolicitacaoResponse(
    Guid Id,
    string TenantId,
    ValePedagioProviderType Provider,
    string ProviderDisplayName,
    ValePedagioIntegrationMode IntegrationMode,
    ValePedagioFlowType FlowType,
    ValePedagioStatus Status,
    string ProviderStatus,
    string TransportadorId,
    string? MotoristaId,
    string? VeiculoId,
    IReadOnlyCollection<string> CteIds,
    ValePedagioRouteDto Route,
    decimal EstimatedCargoValue,
    decimal? ValorTotal,
    string? Protocolo,
    string? NumeroCompra,
    string? DocumentoResponsavelPagamento,
    string? Observacoes,
    string? CallbackUrl,
    bool ReceiptAvailable,
    bool CanPurchase,
    bool CanCancel,
    bool ArtifactsAvailable,
    bool ImportableForMdfe,
    string? FailureReason,
    ValePedagioFailureCategory FailureCategory,
    DateTimeOffset? LastSyncAt,
    DateTimeOffset? NextRetryAt,
    DateTimeOffset? ConcludedAt,
    IReadOnlyCollection<ValePedagioRegulatoryItemDto> ValesPedagio,
    IReadOnlyCollection<ValePedagioAuditTrailDto> AuditTrail,
    IReadOnlyCollection<ValePedagioSyncAttemptDto> SyncAttempts,
    IReadOnlyCollection<ValePedagioProviderArtifactDto> Artifacts,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ValePedagioSolicitacaoListResponse(
    IReadOnlyCollection<ValePedagioSolicitacaoResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);

public interface IValePedagioApplicationService
{
    Task<IReadOnlyCollection<ValePedagioProviderSummaryDto>> ListProvidersAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<ValePedagioProviderConfigurationDto> GetProviderConfigurationAsync(string tenantId, ValePedagioProviderType provider, CancellationToken cancellationToken = default);
    Task<ValePedagioProviderConfigurationDto> UpdateProviderConfigurationAsync(string tenantId, ValePedagioProviderType provider, ValePedagioProviderConfigurationRequest request, CancellationToken cancellationToken = default);
    Task<ValePedagioSolicitacaoListResponse> ListSolicitacoesAsync(string tenantId, ValePedagioProviderType? provider, ValePedagioStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<ValePedagioSolicitacaoResponse?> GetSolicitacaoAsync(string tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<ValePedagioSolicitacaoResponse> QuoteAsync(string tenantId, ValePedagioSolicitacaoRequest request, CancellationToken cancellationToken = default);
    Task<ValePedagioSolicitacaoResponse> PurchaseAsync(string tenantId, ValePedagioSolicitacaoRequest request, CancellationToken cancellationToken = default);
    Task<ValePedagioSolicitacaoResponse> PurchaseAsync(string tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<ValePedagioSolicitacaoResponse> SyncAsync(string tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<ValePedagioSolicitacaoResponse> ProcessCallbackAsync(string tenantId, ValePedagioProviderType provider, ValePedagioProviderCallbackRequest request, CancellationToken cancellationToken = default);
    Task<int> SyncPendingAsync(CancellationToken cancellationToken = default);
    Task<ValePedagioSolicitacaoResponse> CancelAsync(string tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<ValePedagioReceipt?> GetReceiptAsync(string tenantId, Guid id, CancellationToken cancellationToken = default);
}

public sealed class ValePedagioApplicationService : IValePedagioApplicationService
{
    private readonly IValePedagioProviderResolver _providerResolver;
    private readonly IValePedagioSolicitacaoRepository _solicitacaoRepository;
    private readonly IValePedagioProviderConfigurationRepository _configurationRepository;

    public ValePedagioApplicationService(
        IValePedagioProviderResolver providerResolver,
        IValePedagioSolicitacaoRepository solicitacaoRepository,
        IValePedagioProviderConfigurationRepository configurationRepository)
    {
        _providerResolver = providerResolver;
        _solicitacaoRepository = solicitacaoRepository;
        _configurationRepository = configurationRepository;
    }

    public async Task<IReadOnlyCollection<ValePedagioProviderSummaryDto>> ListProvidersAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var catalog = _providerResolver.GetCatalog();
        var result = new List<ValePedagioProviderSummaryDto>(catalog.Count);

        foreach (var descriptor in catalog.OrderBy(item => item.Wave).ThenBy(item => item.DisplayName))
        {
            var config = await _configurationRepository.GetAsync(tenantId, descriptor.Type, cancellationToken);
            result.Add(new ValePedagioProviderSummaryDto(
                descriptor.Type,
                descriptor.DisplayName,
                descriptor.Wave,
                descriptor.Capabilities,
                config.Enabled,
                config.EndpointBaseUrl,
                config.CallbackMode,
                descriptor.IntegrationMode));
        }

        return result;
    }

    public async Task<ValePedagioProviderConfigurationDto> GetProviderConfigurationAsync(string tenantId, ValePedagioProviderType provider, CancellationToken cancellationToken = default)
    {
        var config = await _configurationRepository.GetAsync(tenantId, provider, cancellationToken);
        var descriptor = GetDescriptor(provider);
        return MapConfiguration(config, descriptor);
    }

    public async Task<ValePedagioProviderConfigurationDto> UpdateProviderConfigurationAsync(string tenantId, ValePedagioProviderType provider, ValePedagioProviderConfigurationRequest request, CancellationToken cancellationToken = default)
    {
        var config = await _configurationRepository.GetAsync(tenantId, provider, cancellationToken);
        var mergedPreview = ValePedagioProviderCredentialsValidator.MergePreview(config.Credentials, request.Credentials);
        ValePedagioProviderCredentialsValidator.EnsureCompleteForProvider(provider, mergedPreview);

        config.Enabled = request.Enabled;
        config.EndpointBaseUrl = request.EndpointBaseUrl?.Trim() ?? config.EndpointBaseUrl;
        config.CallbackMode = string.IsNullOrWhiteSpace(request.CallbackMode) ? config.CallbackMode : request.CallbackMode.Trim();
        ValePedagioCredentialMasking.MergeInto(config.Credentials, request.Credentials);
        config.UpdatedAt = DateTimeOffset.UtcNow;

        await _configurationRepository.SaveAsync(config, cancellationToken);
        return MapConfiguration(config, GetDescriptor(provider));
    }

    public async Task<ValePedagioSolicitacaoListResponse> ListSolicitacoesAsync(string tenantId, ValePedagioProviderType? provider, ValePedagioStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var resolvedPage = Math.Max(pageNumber, 1);
        var resolvedPageSize = Math.Clamp(pageSize, 1, 200);
        var solicitacoes = await _solicitacaoRepository.ListAsync(tenantId, cancellationToken);

        var filtered = solicitacoes.AsEnumerable();
        if (provider.HasValue)
        {
            filtered = filtered.Where(item => item.Provider == provider.Value);
        }

        if (status.HasValue)
        {
            filtered = filtered.Where(item => item.Status == status.Value);
        }

        var ordered = filtered.OrderByDescending(item => item.CreatedAt).ToList();
        var pageItems = ordered
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .Select(MapSolicitacao)
            .ToList();

        return new ValePedagioSolicitacaoListResponse(pageItems, resolvedPage, resolvedPageSize, ordered.Count);
    }

    public async Task<ValePedagioSolicitacaoResponse?> GetSolicitacaoAsync(string tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var solicitacao = await _solicitacaoRepository.GetByIdAsync(id, cancellationToken);
        if (solicitacao is null || !string.Equals(solicitacao.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return MapSolicitacao(solicitacao);
    }

    public Task<ValePedagioSolicitacaoResponse> QuoteAsync(string tenantId, ValePedagioSolicitacaoRequest request, CancellationToken cancellationToken = default)
        => ExecuteSolicitacaoAsync(tenantId, request, purchase: false, cancellationToken);

    public Task<ValePedagioSolicitacaoResponse> PurchaseAsync(string tenantId, ValePedagioSolicitacaoRequest request, CancellationToken cancellationToken = default)
        => ExecuteSolicitacaoAsync(tenantId, request, purchase: true, cancellationToken);

    public async Task<ValePedagioSolicitacaoResponse> PurchaseAsync(string tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var solicitacao = await LoadSolicitacaoAsync(tenantId, id, cancellationToken);
        if (!solicitacao.CanPurchase)
        {
            throw new InvalidOperationException("A solicitação atual não pode mais ser convertida em compra.");
        }

        var provider = await _providerResolver.ResolveAsync(tenantId, ValePedagioCapability.Purchase, solicitacao.Provider, cancellationToken);
        var rawRequestPayload = JsonSerializer.Serialize(new { solicitacaoId = solicitacao.Id, provider = solicitacao.Provider, operation = "purchase-from-quote" });

        try
        {
            var result = await provider.PurchaseAsync(solicitacao, cancellationToken);
            solicitacao.ApplyPurchase(result, rawRequestPayload, fromExistingQuote: true);
            await _solicitacaoRepository.AddOrUpdateAsync(solicitacao, cancellationToken);
            return MapSolicitacao(solicitacao);
        }
        catch (Exception ex)
        {
            solicitacao.ApplyFailure("purchase-from-quote", ex.Message, rawRequestPayload, null);
            await _solicitacaoRepository.AddOrUpdateAsync(solicitacao, cancellationToken);
            throw;
        }
    }

    public async Task<ValePedagioSolicitacaoResponse> SyncAsync(string tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var solicitacao = await LoadSolicitacaoAsync(tenantId, id, cancellationToken);
        if (!solicitacao.CanSync)
        {
            throw new InvalidOperationException("A solicitação atual não está em um estado sincronizável.");
        }

        await ExecuteSyncAsync(solicitacao, cancellationToken);
        return MapSolicitacao(solicitacao);
    }

    public async Task<ValePedagioSolicitacaoResponse> ProcessCallbackAsync(string tenantId, ValePedagioProviderType provider, ValePedagioProviderCallbackRequest request, CancellationToken cancellationToken = default)
    {
        var solicitacao = await _solicitacaoRepository.FindAsync(tenantId, provider, request.SolicitacaoId, request.NumeroCompra, request.Protocolo, cancellationToken);
        if (solicitacao is null)
        {
            throw new KeyNotFoundException("Não foi possível localizar a solicitação de vale-pedágio para o callback informado.");
        }

        var rawPayload = string.IsNullOrWhiteSpace(request.RawPayload) ? JsonSerializer.Serialize(request) : request.RawPayload;
        var result = new ValePedagioProviderOperationResult(
            Protocolo: request.Protocolo ?? solicitacao.Protocolo,
            NumeroCompra: request.NumeroCompra ?? solicitacao.NumeroCompra,
            ValorTotal: request.ValorTotal ?? solicitacao.ValorTotal,
            ValesPedagio: solicitacao.RegulatoryItems.ToList(),
            Receipt: TryBuildReceipt(request),
            RawResponsePayload: rawPayload,
            ProviderStatus: request.ProviderStatus,
            SuggestedStatus: MapCallbackStatus(request.ProviderStatus, solicitacao.Status, request.FailureReason),
            FailureReason: request.FailureReason,
            FailureCategory: string.IsNullOrWhiteSpace(request.FailureReason) ? ValePedagioFailureCategory.None : ValePedagioFailureCategory.ProviderRejected,
            NextRetryAt: null);

        solicitacao.ApplyCallback(result, rawPayload);
        await _solicitacaoRepository.AddOrUpdateAsync(solicitacao, cancellationToken);
        return MapSolicitacao(solicitacao);
    }

    public async Task<int> SyncPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _solicitacaoRepository.ListPendingSyncAsync(DateTimeOffset.UtcNow, maxItems: 25, cancellationToken);
        var processed = 0;

        foreach (var solicitacao in pending)
        {
            try
            {
                await ExecuteSyncAsync(solicitacao, cancellationToken);
                processed += 1;
            }
            catch (Exception ex)
            {
                solicitacao.ApplyFailure(
                    "sync",
                    ex.Message,
                    JsonSerializer.Serialize(new { solicitacaoId = solicitacao.Id, provider = solicitacao.Provider, mode = "scheduled" }),
                    null,
                    category: ValePedagioFailureCategory.OperationalPending,
                    nextRetryAt: DateTimeOffset.UtcNow.AddMinutes(5),
                    keepCurrentStatus: true);

                await _solicitacaoRepository.AddOrUpdateAsync(solicitacao, cancellationToken);
            }
        }

        return processed;
    }

    public async Task<ValePedagioSolicitacaoResponse> CancelAsync(string tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var solicitacao = await LoadSolicitacaoAsync(tenantId, id, cancellationToken);
        if (!solicitacao.CanCancel)
        {
            throw new InvalidOperationException("A solicitação já está finalizada e não pode ser cancelada novamente.");
        }

        var provider = await _providerResolver.ResolveAsync(tenantId, ValePedagioCapability.Cancel, solicitacao.Provider, cancellationToken);
        var rawRequestPayload = JsonSerializer.Serialize(new { solicitacaoId = solicitacao.Id, provider = solicitacao.Provider, operation = "cancel" });
        solicitacao.BeginCancellation();

        try
        {
            var result = await provider.CancelAsync(solicitacao, cancellationToken);
            solicitacao.ApplyCancellation(result, rawRequestPayload);
        }
        catch (Exception ex)
        {
            solicitacao.ApplyFailure("cancel", ex.Message, rawRequestPayload, null);
            await _solicitacaoRepository.AddOrUpdateAsync(solicitacao, cancellationToken);
            throw;
        }

        await _solicitacaoRepository.AddOrUpdateAsync(solicitacao, cancellationToken);
        return MapSolicitacao(solicitacao);
    }

    public async Task<ValePedagioReceipt?> GetReceiptAsync(string tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var solicitacao = await _solicitacaoRepository.GetByIdAsync(id, cancellationToken);
        if (solicitacao is null || !string.Equals(solicitacao.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (solicitacao.Receipt is null)
        {
            var provider = await _providerResolver.ResolveAsync(tenantId, ValePedagioCapability.Receipt, solicitacao.Provider, cancellationToken);
            var receipt = await provider.GetReceiptAsync(solicitacao, cancellationToken);
            if (receipt is not null)
            {
                solicitacao.SetReceipt(receipt);
                await _solicitacaoRepository.AddOrUpdateAsync(solicitacao, cancellationToken);
            }
        }

        return solicitacao.Receipt;
    }

    private async Task<ValePedagioSolicitacaoResponse> ExecuteSolicitacaoAsync(string tenantId, ValePedagioSolicitacaoRequest request, bool purchase, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var provider = await _providerResolver.ResolveAsync(
            tenantId,
            purchase ? ValePedagioCapability.Purchase : ValePedagioCapability.Quote,
            request.PreferredProvider,
            cancellationToken);

        var solicitacao = new ValePedagioSolicitacao(
            Guid.NewGuid(),
            tenantId,
            provider.Descriptor.Type,
            provider.Descriptor.IntegrationMode,
            purchase ? ValePedagioFlowType.QuoteAndPurchase : ValePedagioFlowType.QuoteOnly,
            request.TransportadorId.Trim(),
            request.MotoristaId?.Trim(),
            request.VeiculoId?.Trim(),
            request.CteIds,
            new ValePedagioRoute(
                request.Route.UfOrigem.Trim().ToUpperInvariant(),
                request.Route.UfDestino.Trim().ToUpperInvariant(),
                request.Route.UfsPercurso.Select(static item => item.Trim().ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                request.Route.PontosParada.Select(static item => item.Trim()).Where(static item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()),
            request.EstimatedCargoValue,
            request.DocumentoResponsavelPagamento?.Trim(),
            request.Observacoes?.Trim(),
            request.CallbackUrl?.Trim());

        var context = new ValePedagioProviderOperationContext(
            tenantId,
            solicitacao.TransportadorId,
            solicitacao.MotoristaId,
            solicitacao.VeiculoId,
            solicitacao.CteIds,
            solicitacao.Route,
            solicitacao.EstimatedCargoValue,
            solicitacao.DocumentoResponsavelPagamento,
            solicitacao.CallbackUrl,
            solicitacao.Observacoes);

        var rawRequestPayload = JsonSerializer.Serialize(request);

        try
        {
            var result = purchase
                ? await provider.PurchaseAsync(context, cancellationToken)
                : await provider.QuoteAsync(context, cancellationToken);

            if (purchase)
            {
                solicitacao.ApplyPurchase(result, rawRequestPayload);
            }
            else
            {
                solicitacao.ApplyQuote(result, rawRequestPayload);
            }
        }
        catch (Exception ex)
        {
            solicitacao.ApplyFailure(purchase ? "purchase" : "quote", ex.Message, rawRequestPayload, null);
            await _solicitacaoRepository.AddOrUpdateAsync(solicitacao, cancellationToken);
            throw;
        }

        await _solicitacaoRepository.AddOrUpdateAsync(solicitacao, cancellationToken);
        return MapSolicitacao(solicitacao);
    }

    private async Task ExecuteSyncAsync(ValePedagioSolicitacao solicitacao, CancellationToken cancellationToken)
    {
        var provider = await _providerResolver.ResolveAsync(solicitacao.TenantId, ValePedagioCapability.Sync, solicitacao.Provider, cancellationToken);
        var rawRequestPayload = JsonSerializer.Serialize(new { solicitacaoId = solicitacao.Id, provider = solicitacao.Provider, operation = "sync" });
        var result = await provider.SyncAsync(solicitacao, cancellationToken);
        solicitacao.ApplySync(result, rawRequestPayload);
        await _solicitacaoRepository.AddOrUpdateAsync(solicitacao, cancellationToken);
    }

    private async Task<ValePedagioSolicitacao> LoadSolicitacaoAsync(string tenantId, Guid id, CancellationToken cancellationToken)
    {
        var solicitacao = await _solicitacaoRepository.GetByIdAsync(id, cancellationToken);
        if (solicitacao is null || !string.Equals(solicitacao.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            throw new KeyNotFoundException($"Solicitação {id} não encontrada para o tenant informado.");
        }

        return solicitacao;
    }

    private static void ValidateRequest(ValePedagioSolicitacaoRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TransportadorId);
        if (string.IsNullOrWhiteSpace(request.MotoristaId))
        {
            throw new ArgumentException("Motorista é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(request.VeiculoId))
        {
            throw new ArgumentException("Veículo é obrigatório.");
        }

        if (request.CteIds is null || request.CteIds.Count == 0)
        {
            throw new ArgumentException("Informe ao menos um CT-e para montar a solicitação.");
        }

        if (request.Route is null)
        {
            throw new ArgumentException("Rota é obrigatória.");
        }

        if (string.IsNullOrWhiteSpace(request.Route.UfOrigem) || string.IsNullOrWhiteSpace(request.Route.UfDestino))
        {
            throw new ArgumentException("UF de origem e destino são obrigatórias.");
        }
    }

    private ValePedagioProviderDescriptor GetDescriptor(ValePedagioProviderType provider)
    {
        return _providerResolver.GetCatalog().Single(item => item.Type == provider);
    }

    private ValePedagioProviderConfigurationDto MapConfiguration(ValePedagioProviderConfiguration config, ValePedagioProviderDescriptor descriptor)
    {
        return new ValePedagioProviderConfigurationDto(
            config.TenantId,
            config.Provider,
            descriptor.DisplayName,
            descriptor.Wave,
            descriptor.Capabilities,
            config.Enabled,
            config.EndpointBaseUrl,
            config.CallbackMode,
            ValePedagioCredentialMasking.MaskForDisplay(config.Credentials),
            config.UpdatedAt,
            descriptor.IntegrationMode);
    }

    private ValePedagioSolicitacaoResponse MapSolicitacao(ValePedagioSolicitacao solicitacao)
    {
        var descriptor = GetDescriptor(solicitacao.Provider);
        return new ValePedagioSolicitacaoResponse(
            solicitacao.Id,
            solicitacao.TenantId,
            solicitacao.Provider,
            descriptor.DisplayName,
            solicitacao.IntegrationMode,
            solicitacao.FlowType,
            solicitacao.Status,
            solicitacao.ProviderStatus,
            solicitacao.TransportadorId,
            solicitacao.MotoristaId,
            solicitacao.VeiculoId,
            solicitacao.CteIds.ToList(),
            new ValePedagioRouteDto(
                solicitacao.Route.UfOrigem,
                solicitacao.Route.UfDestino,
                solicitacao.Route.UfsPercurso.ToList(),
                solicitacao.Route.PontosParada.ToList()),
            solicitacao.EstimatedCargoValue,
            solicitacao.ValorTotal,
            solicitacao.Protocolo,
            solicitacao.NumeroCompra,
            solicitacao.DocumentoResponsavelPagamento,
            solicitacao.Observacoes,
            solicitacao.CallbackUrl,
            solicitacao.Receipt is not null,
            solicitacao.CanPurchase,
            solicitacao.CanCancel,
            solicitacao.ArtifactsAvailable,
            solicitacao.IsImportableForMdfe,
            solicitacao.FailureReason,
            solicitacao.FailureCategory,
            solicitacao.LastSyncAt,
            solicitacao.NextRetryAt,
            solicitacao.ConcludedAt,
            solicitacao.RegulatoryItems
                .Select(static item => new ValePedagioRegulatoryItemDto(
                    item.CnpjFornecedor,
                    item.DocumentoResponsavelPagamento,
                    item.NumeroCompra,
                    item.ValorValePedagio,
                    item.TipoValePedagio))
                .ToList(),
            solicitacao.AuditTrail
                .Select(static item => new ValePedagioAuditTrailDto(
                    item.Operation,
                    item.RequestPayload,
                    item.ResponsePayload,
                    item.OccurredAt))
                .ToList(),
            solicitacao.SyncAttempts
                .Select(static item => new ValePedagioSyncAttemptDto(
                    item.Operation,
                    item.Successful,
                    item.RequestPayload,
                    item.ResponsePayload,
                    item.Message,
                    item.OccurredAt))
                .ToList(),
            solicitacao.ProviderArtifacts
                .Select(static item => new ValePedagioProviderArtifactDto(
                    item.Operation,
                    item.ArtifactType,
                    item.FileName,
                    item.ContentType,
                    item.OccurredAt))
                .ToList(),
            solicitacao.CreatedAt,
            solicitacao.UpdatedAt);
    }

    private static ValePedagioReceipt? TryBuildReceipt(ValePedagioProviderCallbackRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ReceiptBase64))
        {
            return null;
        }

        try
        {
            return new ValePedagioReceipt(
                request.ReceiptFileName ?? $"vale-pedagio-callback-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.bin",
                request.ReceiptContentType ?? "application/octet-stream",
                Convert.FromBase64String(request.ReceiptBase64),
                DateTimeOffset.UtcNow);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static ValePedagioStatus MapCallbackStatus(string? providerStatus, ValePedagioStatus currentStatus, string? failureReason)
    {
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return ValePedagioStatus.Recusado;
        }

        if (string.IsNullOrWhiteSpace(providerStatus))
        {
            return currentStatus;
        }

        return providerStatus.Trim().ToLowerInvariant() switch
        {
            "quoted" or "cotado" => ValePedagioStatus.Cotado,
            "purchased" or "comprado" => ValePedagioStatus.Comprado,
            "confirmed" or "confirmado" => ValePedagioStatus.Confirmado,
            "route_without_cost" or "rota_sem_custo" => ValePedagioStatus.RotaSemCusto,
            "route_registration_pending" or "aguardando_cadastro_rota" => ValePedagioStatus.AguardandoCadastroRota,
            "rejected" or "recusado" => ValePedagioStatus.Recusado,
            "cancellation_pending" or "em_cancelamento" => ValePedagioStatus.EmCancelamento,
            "cancelled" or "cancelado" => ValePedagioStatus.Cancelado,
            "closed" or "encerrado" => ValePedagioStatus.Encerrado,
            "failed" or "falha" => ValePedagioStatus.Falha,
            _ => currentStatus
        };
    }
}
