using Microsoft.EntityFrameworkCore;
using Moq;
using ValePedagio.Domain;
using ValePedagio.Infrastructure;
using ValePedagio.Infrastructure.Persistence;

namespace ValePedagio.Tests;

public sealed class CatalogAndFactoryTests
{
    [Fact]
    public void Descriptors_DeveConterTodosOsProviders()
    {
        var types = ValePedagioProviderCatalog.Descriptors.Select(d => d.Type).ToHashSet();
        foreach (var t in Enum.GetValues<ValePedagioProviderType>())
        {
            Assert.Contains(t, types);
        }
    }

    [Fact]
    public void Documents_DeveCobrirTodosOsProviders()
    {
        foreach (var t in Enum.GetValues<ValePedagioProviderType>())
        {
            Assert.True(ValePedagioProviderDocuments.Documents.ContainsKey(t));
        }
    }

    [Fact]
    public void CreateDefault_EFrete_DeveTerCredenciaisFiscaisCompletas()
    {
        var c = ValePedagioProviderConfigurationFactory.CreateDefault("tenant", ValePedagioProviderType.EFrete);
        Assert.True(c.Enabled);
        Assert.Equal("polling", c.CallbackMode);
        Assert.Contains("integratorHash", c.Credentials.Keys);
        Assert.Contains("logonOperation", c.Credentials.Keys);
        Assert.Contains("quoteAction", c.Credentials.Keys);
    }

    [Theory]
    [InlineData(ValePedagioProviderType.Ambipar, "polling")]
    [InlineData(ValePedagioProviderType.SemParar, "webhook")]
    [InlineData(ValePedagioProviderType.Pamcard, "webhook")]
    [InlineData(ValePedagioProviderType.QualP, "polling")]
    public void CreateDefault_NonEFrete_RespeitaCallbackMode(ValePedagioProviderType type, string expected)
    {
        var c = ValePedagioProviderConfigurationFactory.CreateDefault("tenant", type);
        Assert.Equal(expected, c.CallbackMode);
        Assert.Contains("clientId", c.Credentials.Keys);
        Assert.Contains("apiKey", c.Credentials.Keys);
        Assert.Contains("providerDocument", c.Credentials.Keys);
    }
}

public sealed class ProviderResolverTests
{
    private static IValePedagioProviderConfigurationRepository RepoWithEnabled(params ValePedagioProviderType[] enabled)
    {
        var mock = new Mock<IValePedagioProviderConfigurationRepository>();
        mock.Setup(m => m.GetAsync(It.IsAny<string>(), It.IsAny<ValePedagioProviderType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string tenant, ValePedagioProviderType p, CancellationToken _) =>
            {
                var cfg = ValePedagioProviderConfigurationFactory.CreateDefault(tenant, p);
                cfg.Enabled = enabled.Contains(p);
                return cfg;
            });
        return mock.Object;
    }

    private static List<IValePedagioProvider> Providers()
        => ValePedagioProviderCatalog.Descriptors
            .Select(d => (IValePedagioProvider)new CatalogValePedagioProvider(d))
            .ToList();

    [Fact]
    public async Task ResolveAsync_SemPreferencia_DevolvePrimeiroHabilitado()
    {
        var resolver = new ValePedagioProviderResolver(Providers(), RepoWithEnabled(ValePedagioProviderType.Repom));
        var p = await resolver.ResolveAsync("t", ValePedagioCapability.Quote, null);
        Assert.Equal(ValePedagioProviderType.Repom, p.Descriptor.Type);
    }

    [Fact]
    public async Task ResolveAsync_SemPreferenciaENenhumHabilitado_Lanca()
    {
        var resolver = new ValePedagioProviderResolver(Providers(), RepoWithEnabled());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resolver.ResolveAsync("t", ValePedagioCapability.Quote, null));
    }

    [Fact]
    public async Task ResolveAsync_PreferidoDesabilitado_Lanca()
    {
        var resolver = new ValePedagioProviderResolver(Providers(), RepoWithEnabled());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resolver.ResolveAsync("t", ValePedagioCapability.Quote, ValePedagioProviderType.EFrete));
    }

    [Fact]
    public async Task ResolveAsync_PreferidoSemCapability_Lanca()
    {
        // EFrete não tem Capability.Callback
        var resolver = new ValePedagioProviderResolver(Providers(), RepoWithEnabled(ValePedagioProviderType.EFrete));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resolver.ResolveAsync("t", ValePedagioCapability.Callback, ValePedagioProviderType.EFrete));
    }

    [Fact]
    public async Task ResolveAsync_PreferidoHabilitado_Devolve()
    {
        var resolver = new ValePedagioProviderResolver(Providers(), RepoWithEnabled(ValePedagioProviderType.Ambipar));
        var p = await resolver.ResolveAsync("t", ValePedagioCapability.Quote, ValePedagioProviderType.Ambipar);
        Assert.Equal(ValePedagioProviderType.Ambipar, p.Descriptor.Type);
    }

    [Fact]
    public void GetCatalog_DevolveDescritores()
    {
        var resolver = new ValePedagioProviderResolver(Providers(), RepoWithEnabled());
        Assert.NotEmpty(resolver.GetCatalog());
    }
}

public sealed class CatalogValePedagioProviderTests
{
    private static ValePedagioProviderOperationContext Ctx() => new(
        "t", "trans", "moto", "vei",
        new[] { "cte-1", "cte-2" },
        new ValePedagioRoute("SP", "RJ", new[] { "MG" }, new[] { "P1", "P2" }),
        100m, "doc", "https://cb", "obs");

    [Fact]
    public async Task QuoteAsync_DeveProduzirResultadoCotado()
    {
        var p = new CatalogValePedagioProvider(ValePedagioProviderCatalog.Descriptors.First(d => d.Type == ValePedagioProviderType.Ambipar));
        var r = await p.QuoteAsync(Ctx());
        Assert.Equal(ValePedagioStatus.Cotado, r.SuggestedStatus);
        Assert.NotNull(r.NumeroCompra);
        Assert.NotNull(r.Protocolo);
        Assert.True(r.ValorTotal > 0);
        Assert.Single(r.ValesPedagio);
    }

    [Fact]
    public async Task PurchaseAsync_DeveIncluirReceipt()
    {
        var p = new CatalogValePedagioProvider(ValePedagioProviderCatalog.Descriptors.First(d => d.Type == ValePedagioProviderType.Repom));
        var r = await p.PurchaseAsync(Ctx());
        Assert.Equal(ValePedagioStatus.Comprado, r.SuggestedStatus);
        Assert.NotNull(r.Receipt);
        Assert.Equal("Cartao", r.ValesPedagio.First().TipoValePedagio);
    }

    [Fact]
    public async Task PurchaseAsync_DeSolicitacao_ReusaProtocolo()
    {
        var p = new CatalogValePedagioProvider(ValePedagioProviderCatalog.Descriptors.First(d => d.Type == ValePedagioProviderType.SemParar));
        var sol = new ValePedagioSolicitacao(Guid.NewGuid(), "t", ValePedagioProviderType.SemParar, ValePedagioIntegrationMode.Real,
            ValePedagioFlowType.QuoteOnly, "trans", "moto", "vei", new[] { "cte-1" },
            new ValePedagioRoute("SP", "RJ", new[] { "MG" }, new[] { "P1" }), 100m, "doc", "obs", "https://cb");
        var quoteResult = await p.QuoteAsync(new ValePedagioProviderOperationContext(sol.TenantId, sol.TransportadorId, sol.MotoristaId, sol.VeiculoId, sol.CteIds, sol.Route, sol.EstimatedCargoValue, sol.DocumentoResponsavelPagamento, sol.CallbackUrl, sol.Observacoes));
        sol.ApplyQuote(quoteResult, "{}");
        var r = await p.PurchaseAsync(sol);
        Assert.Equal(sol.NumeroCompra, r.NumeroCompra);
    }

    [Fact]
    public async Task SyncAsync_EmCancelamento_DeveIrParaCancelado()
    {
        var p = new CatalogValePedagioProvider(ValePedagioProviderCatalog.Descriptors.First(d => d.Type == ValePedagioProviderType.QualP));
        var sol = new ValePedagioSolicitacao(Guid.NewGuid(), "t", ValePedagioProviderType.QualP, ValePedagioIntegrationMode.Real,
            ValePedagioFlowType.QuoteAndPurchase, "tr", "m", "v", new[] { "c1" },
            new ValePedagioRoute("SP", "RJ", Array.Empty<string>(), Array.Empty<string>()), 100m, null, null, null);
        sol.BeginCancellation();
        var r = await p.SyncAsync(sol);
        Assert.Equal(ValePedagioStatus.Cancelado, r.SuggestedStatus);
    }

    [Fact]
    public async Task SyncAsync_Comprado_DeveIrParaConfirmado()
    {
        var p = new CatalogValePedagioProvider(ValePedagioProviderCatalog.Descriptors.First(d => d.Type == ValePedagioProviderType.QualP));
        var sol = new ValePedagioSolicitacao(Guid.NewGuid(), "t", ValePedagioProviderType.QualP, ValePedagioIntegrationMode.Real,
            ValePedagioFlowType.QuoteAndPurchase, "tr", "m", "v", new[] { "c1" },
            new ValePedagioRoute("SP", "RJ", Array.Empty<string>(), Array.Empty<string>()), 100m, null, null, null);
        sol.ApplyPurchase(new ValePedagioProviderOperationResult("P", "N", 10m, Array.Empty<ValePedagioRegulatoryItem>(), null, "{}", "purchased", ValePedagioStatus.Comprado), "{}");
        var r = await p.SyncAsync(sol);
        Assert.Equal(ValePedagioStatus.Confirmado, r.SuggestedStatus);
        Assert.NotNull(r.Receipt);
    }

    [Fact]
    public async Task CancelAsync_DeveDevolverStatusCancelado()
    {
        var p = new CatalogValePedagioProvider(ValePedagioProviderCatalog.Descriptors.First(d => d.Type == ValePedagioProviderType.Pamcard));
        var sol = new ValePedagioSolicitacao(Guid.NewGuid(), "t", ValePedagioProviderType.Pamcard, ValePedagioIntegrationMode.Real,
            ValePedagioFlowType.QuoteAndPurchase, "tr", "m", "v", new[] { "c1" },
            new ValePedagioRoute("SP", "RJ", Array.Empty<string>(), Array.Empty<string>()), 100m, null, null, null);
        var r = await p.CancelAsync(sol);
        Assert.Equal(ValePedagioStatus.Cancelado, r.SuggestedStatus);
        Assert.NotNull(r.NumeroCompra);
    }

    [Fact]
    public async Task GetReceiptAsync_SemNumeroCompra_RetornaNull()
    {
        var p = new CatalogValePedagioProvider(ValePedagioProviderCatalog.Descriptors.First(d => d.Type == ValePedagioProviderType.Target));
        var sol = new ValePedagioSolicitacao(Guid.NewGuid(), "t", ValePedagioProviderType.Target, ValePedagioIntegrationMode.Real,
            ValePedagioFlowType.QuoteAndPurchase, "tr", "m", "v", new[] { "c1" },
            new ValePedagioRoute("SP", "RJ", Array.Empty<string>(), Array.Empty<string>()), 100m, null, null, null);
        var r = await p.GetReceiptAsync(sol);
        Assert.Null(r);
    }

    [Fact]
    public async Task GetReceiptAsync_ComReciboExistente_RetornaMesmo()
    {
        var p = new CatalogValePedagioProvider(ValePedagioProviderCatalog.Descriptors.First(d => d.Type == ValePedagioProviderType.Target));
        var sol = new ValePedagioSolicitacao(Guid.NewGuid(), "t", ValePedagioProviderType.Target, ValePedagioIntegrationMode.Real,
            ValePedagioFlowType.QuoteAndPurchase, "tr", "m", "v", new[] { "c1" },
            new ValePedagioRoute("SP", "RJ", Array.Empty<string>(), Array.Empty<string>()), 100m, null, null, null);
        var existing = new ValePedagioReceipt("r.txt", "text/plain", new byte[] { 1 }, DateTimeOffset.UtcNow);
        sol.SetReceipt(existing);
        var r = await p.GetReceiptAsync(sol);
        Assert.Same(existing, r);
    }

    [Fact]
    public async Task GetReceiptAsync_ComNumeroEValor_GeraRecibo()
    {
        var p = new CatalogValePedagioProvider(ValePedagioProviderCatalog.Descriptors.First(d => d.Type == ValePedagioProviderType.Target));
        var sol = new ValePedagioSolicitacao(Guid.NewGuid(), "t", ValePedagioProviderType.Target, ValePedagioIntegrationMode.Real,
            ValePedagioFlowType.QuoteAndPurchase, "tr", "m", "v", new[] { "c1" },
            new ValePedagioRoute("SP", "RJ", new[] { "MG" }, new[] { "P1" }), 100m, "doc", null, null);
        sol.ApplyPurchase(new ValePedagioProviderOperationResult("P", "N", 50m, Array.Empty<ValePedagioRegulatoryItem>(), null, "{}", "purchased", ValePedagioStatus.Comprado), "{}");
        // Reset receipt to null to force generation
        var noReceiptSol = new ValePedagioSolicitacao(Guid.NewGuid(), "t", ValePedagioProviderType.Target, ValePedagioIntegrationMode.Real,
            ValePedagioFlowType.QuoteAndPurchase, "tr", "m", "v", new[] { "c1" },
            new ValePedagioRoute("SP", "RJ", new[] { "MG" }, new[] { "P1" }), 100m, "doc", null, null);
        noReceiptSol.ApplyPurchase(new ValePedagioProviderOperationResult("P", "N", 50m, Array.Empty<ValePedagioRegulatoryItem>(), null, "{}", "purchased", ValePedagioStatus.Comprado), "{}");
        var r = await p.GetReceiptAsync(noReceiptSol);
        Assert.NotNull(r);
    }
}

public sealed class PostgresRepositoryAdditionalTests
{
    private static ValePedagioDbContext CreateDb(string name)
    {
        var opts = new DbContextOptionsBuilder<ValePedagioDbContext>().UseInMemoryDatabase(name).Options;
        return new ValePedagioDbContext(opts);
    }

    [Fact]
    public async Task SolicitacaoRepository_AddOrUpdate_E_Get()
    {
        await using var db = CreateDb(nameof(SolicitacaoRepository_AddOrUpdate_E_Get));
        var repo = new PostgresValePedagioSolicitacaoRepository(db);
        var sol = new ValePedagioSolicitacao(Guid.NewGuid(), "t1", ValePedagioProviderType.EFrete,
            ValePedagioIntegrationMode.Real, ValePedagioFlowType.QuoteOnly, "tr", "m", "v",
            new[] { "c1" }, new ValePedagioRoute("SP", "RJ", Array.Empty<string>(), Array.Empty<string>()),
            100m, null, null, null);
        await repo.AddOrUpdateAsync(sol);
        var loaded = await repo.GetByIdAsync(sol.Id);
        Assert.NotNull(loaded);
        Assert.Equal("t1", loaded!.TenantId);

        var list = await repo.ListAsync("t1");
        Assert.Single(list);
    }

    [Fact]
    public async Task SolicitacaoRepository_FindAsync_PorNumeroCompra()
    {
        await using var db = CreateDb(nameof(SolicitacaoRepository_FindAsync_PorNumeroCompra));
        var repo = new PostgresValePedagioSolicitacaoRepository(db);
        var sol = new ValePedagioSolicitacao(Guid.NewGuid(), "t1", ValePedagioProviderType.EFrete,
            ValePedagioIntegrationMode.Real, ValePedagioFlowType.QuoteAndPurchase, "tr", "m", "v",
            new[] { "c1" }, new ValePedagioRoute("SP", "RJ", Array.Empty<string>(), Array.Empty<string>()),
            100m, null, null, null);
        sol.ApplyPurchase(new ValePedagioProviderOperationResult("P-X", "N-X", 10m, Array.Empty<ValePedagioRegulatoryItem>(), null, "{}", "purchased", ValePedagioStatus.Comprado), "{}");
        await repo.AddOrUpdateAsync(sol);

        var found = await repo.FindAsync("t1", ValePedagioProviderType.EFrete, null, "N-X", null);
        Assert.NotNull(found);
        Assert.Equal(sol.Id, found!.Id);

        var foundByProto = await repo.FindAsync("t1", ValePedagioProviderType.EFrete, null, null, "P-X");
        Assert.NotNull(foundByProto);

        var notFound = await repo.FindAsync("other", ValePedagioProviderType.EFrete, null, "N-X", null);
        Assert.Null(notFound);
    }

    [Fact]
    public async Task ConfigurationRepository_ListAsync()
    {
        await using var db = CreateDb(nameof(ConfigurationRepository_ListAsync));
        var repo = new PostgresValePedagioProviderConfigurationRepository(db);
        await repo.GetAsync("t1", ValePedagioProviderType.EFrete);
        await repo.GetAsync("t1", ValePedagioProviderType.Ambipar);
        var list = await repo.ListAsync("t1");
        Assert.True(list.Count >= 2);
    }
}
