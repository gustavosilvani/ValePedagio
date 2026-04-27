using ValePedagio.Domain;

namespace ValePedagio.Tests;

public sealed class DomainTests
{
    private static ValePedagioSolicitacao CreateSolicitacao(
        ValePedagioStatus? overrideStatus = null,
        ValePedagioProviderType provider = ValePedagioProviderType.EFrete)
    {
        var sol = new ValePedagioSolicitacao(
            Guid.NewGuid(),
            "tenant-1",
            provider,
            ValePedagioIntegrationMode.Real,
            ValePedagioFlowType.QuoteOnly,
            "trans-1",
            "moto-1",
            "vei-1",
            new[] { "cte-1", "cte-2", "", "cte-1" },
            new ValePedagioRoute("SP", "RJ", new[] { "MG" }, new[] { "P1" }),
            100m,
            "doc",
            "obs",
            "https://cb");
        return sol;
    }

    private static ValePedagioProviderOperationResult Result(
        ValePedagioStatus? suggested = null,
        string? failure = null,
        ValePedagioFailureCategory? cat = null,
        ValePedagioReceipt? receipt = null,
        string? providerStatus = null,
        decimal? valor = 50m,
        string? protocolo = "PROTO",
        string? numero = "NUM")
    {
        return new ValePedagioProviderOperationResult(
            protocolo,
            numero,
            valor,
            new[]
            {
                new ValePedagioRegulatoryItem("11", null, "NUM", 50m, "TAG")
            },
            receipt,
            "{\"raw\":1}",
            providerStatus,
            suggested,
            failure,
            cat);
    }

    [Fact]
    public void Construtor_DeveDeduplicarECteIdsERemoverVazios()
    {
        var sol = CreateSolicitacao();
        Assert.Equal(2, sol.CteIds.Count);
        Assert.Contains("cte-1", sol.CteIds);
        Assert.Contains("cte-2", sol.CteIds);
        Assert.Equal(ValePedagioStatus.EmProcessamento, sol.Status);
        Assert.Equal("processing", sol.ProviderStatus);
        Assert.Equal(ValePedagioFailureCategory.None, sol.FailureCategory);
    }

    [Fact]
    public void CanPurchase_True_QuandoCotadoOuEmProcessamento()
    {
        var sol = CreateSolicitacao();
        Assert.True(sol.CanPurchase);
        sol.ApplyQuote(Result(ValePedagioStatus.Cotado), "{}");
        Assert.True(sol.CanPurchase);
        sol.ApplyPurchase(Result(ValePedagioStatus.Comprado), "{}");
        Assert.False(sol.CanPurchase);
    }

    [Fact]
    public void CanCancel_FalseQuandoCanceladoEncerradoOuFalha()
    {
        var sol = CreateSolicitacao();
        sol.ApplyPurchase(Result(ValePedagioStatus.Comprado), "{}");
        Assert.True(sol.CanCancel);
        sol.ApplyFailure("op", "x", null, null);
        Assert.False(sol.CanCancel);
    }

    [Fact]
    public void ApplyQuote_DeveSetarStatusCotadoEFlowQuoteOnly()
    {
        var sol = CreateSolicitacao();
        sol.ApplyQuote(Result(ValePedagioStatus.Cotado, providerStatus: "quoted"), "{\"r\":1}");
        Assert.Equal(ValePedagioStatus.Cotado, sol.Status);
        Assert.Equal(ValePedagioFlowType.QuoteOnly, sol.FlowType);
        Assert.Equal("quoted", sol.ProviderStatus);
        Assert.Equal("PROTO", sol.Protocolo);
        Assert.NotEmpty(sol.AuditTrail);
        Assert.NotEmpty(sol.SyncAttempts);
    }

    [Fact]
    public void ApplyPurchase_FromExistingQuote_DeveAjustarFlow()
    {
        var sol = CreateSolicitacao();
        sol.ApplyQuote(Result(ValePedagioStatus.Cotado), "{}");
        sol.ApplyPurchase(Result(ValePedagioStatus.Comprado), "{}", fromExistingQuote: true);
        Assert.Equal(ValePedagioFlowType.QuoteAndPurchase, sol.FlowType);
        Assert.Equal(ValePedagioStatus.Comprado, sol.Status);
    }

    [Fact]
    public void ApplyPurchase_DeveUsarStatusFallbackQuandoSemSugestao()
    {
        var sol = CreateSolicitacao();
        sol.ApplyPurchase(Result(suggested: null, providerStatus: null), "{}");
        Assert.Equal(ValePedagioStatus.Comprado, sol.Status);
        Assert.Equal("purchased", sol.ProviderStatus);
    }

    [Fact]
    public void ApplyPurchase_StatusConcluidos_DeveSetarConcludedAt()
    {
        var sol = CreateSolicitacao();
        sol.ApplyPurchase(Result(ValePedagioStatus.Confirmado), "{}");
        Assert.NotNull(sol.ConcludedAt);
        Assert.Equal(ValePedagioStatus.Confirmado, sol.Status);
    }

    [Fact]
    public void BeginCancellation_DeveSetarStatusEmCancelamento()
    {
        var sol = CreateSolicitacao();
        sol.BeginCancellation();
        Assert.Equal(ValePedagioStatus.EmCancelamento, sol.Status);
        Assert.Equal("cancellation_pending", sol.ProviderStatus);
        Assert.Null(sol.FailureReason);
    }

    [Fact]
    public void ApplyCancellation_DeveSetarCanceladoEConcluido()
    {
        var sol = CreateSolicitacao();
        sol.BeginCancellation();
        sol.ApplyCancellation(Result(ValePedagioStatus.Cancelado), "{}");
        Assert.Equal(ValePedagioStatus.Cancelado, sol.Status);
        Assert.NotNull(sol.ConcludedAt);
    }

    [Fact]
    public void ApplySync_DeveAtualizarLastSyncAt()
    {
        var sol = CreateSolicitacao();
        sol.ApplyPurchase(Result(ValePedagioStatus.Comprado), "{}");
        sol.ApplySync(Result(ValePedagioStatus.Confirmado), "{}");
        Assert.NotNull(sol.LastSyncAt);
    }

    [Fact]
    public void ApplyCallback_DeveAtualizarLastSyncAt()
    {
        var sol = CreateSolicitacao();
        sol.ApplyCallback(Result(ValePedagioStatus.Comprado), "{}");
        Assert.NotNull(sol.LastSyncAt);
        Assert.Contains(sol.AuditTrail, a => a.Operation == "callback");
    }

    [Fact]
    public void ApplyFailure_DeveSetarFalhaIncrementarRetry()
    {
        var sol = CreateSolicitacao();
        sol.ApplyFailure("quote", "erro", "{req}", "{resp}");
        Assert.Equal(ValePedagioStatus.Falha, sol.Status);
        Assert.Equal("erro", sol.FailureReason);
        Assert.Equal(1, sol.RetryCount);
        Assert.NotNull(sol.ConcludedAt);
    }

    [Fact]
    public void ApplyFailure_KeepCurrentStatus_DeveManterStatus()
    {
        var sol = CreateSolicitacao();
        sol.ApplyPurchase(Result(ValePedagioStatus.Comprado), "{}");
        sol.ApplyFailure("sync", "tmp", null, null,
            ValePedagioFailureCategory.OperationalPending,
            DateTimeOffset.UtcNow.AddMinutes(5),
            keepCurrentStatus: true);
        Assert.Equal(ValePedagioStatus.Comprado, sol.Status);
        Assert.Equal(ValePedagioFailureCategory.OperationalPending, sol.FailureCategory);
        Assert.NotNull(sol.NextRetryAt);
    }

    [Fact]
    public void SetReceipt_DeveSalvarReciboEAdicionarArtefato()
    {
        var sol = CreateSolicitacao();
        var receipt = new ValePedagioReceipt("rec.txt", "text/plain", new byte[] { 1, 2 }, DateTimeOffset.UtcNow);
        sol.SetReceipt(receipt);
        Assert.NotNull(sol.Receipt);
        Assert.True(sol.ArtifactsAvailable);
        Assert.Contains(sol.ProviderArtifacts, a => a.ArtifactType == ValePedagioArtifactType.Receipt);
    }

    [Fact]
    public void IsImportableForMdfe_TrueParaCompradoOuConfirmado()
    {
        var sol = CreateSolicitacao();
        sol.ApplyPurchase(Result(ValePedagioStatus.Comprado), "{}");
        Assert.True(sol.IsImportableForMdfe);
    }

    [Fact]
    public void CanSync_DeveCobrirEstadosEsperados()
    {
        var sol = CreateSolicitacao();
        Assert.True(sol.CanSync); // EmProcessamento
        sol.ApplyPurchase(Result(ValePedagioStatus.Comprado), "{}");
        Assert.True(sol.CanSync);
        sol.ApplyCancellation(Result(ValePedagioStatus.Cancelado), "{}");
        Assert.False(sol.CanSync);
    }

    [Fact]
    public void ApplyProviderResult_ComReceiptSeparado_DeveAdicionarArtefatoRecibo()
    {
        var sol = CreateSolicitacao();
        var receipt = new ValePedagioReceipt("a.bin", "application/octet-stream", new byte[] { 9 }, DateTimeOffset.UtcNow);
        sol.ApplyPurchase(Result(ValePedagioStatus.Comprado, receipt: receipt), "{}");
        Assert.NotNull(sol.Receipt);
        Assert.Contains(sol.ProviderArtifacts, a => a.ArtifactType == ValePedagioArtifactType.Receipt);
    }
}
