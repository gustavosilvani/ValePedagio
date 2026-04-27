using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ValePedagio.Domain;
using ValePedagio.Infrastructure;
using ValePedagio.Infrastructure.Providers;

namespace ValePedagio.Tests;

internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _factories;
    public List<HttpRequestMessage> Requests { get; } = [];

    public StubHttpHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] factories)
    {
        _factories = new(factories);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (_factories.Count == 0)
            throw new InvalidOperationException("Sem respostas configuradas.");
        return Task.FromResult(_factories.Dequeue()(request));
    }
}

public sealed class RestProvidersTests
{
    private static HttpResponseMessage Json(HttpStatusCode code, string body)
        => new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static IValePedagioProviderConfigurationRepository RepoFor(ValePedagioProviderType type, string tenant = "t1")
    {
        var mock = new Mock<IValePedagioProviderConfigurationRepository>();
        mock.Setup(m => m.GetAsync(It.IsAny<string>(), It.IsAny<ValePedagioProviderType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string t, ValePedagioProviderType p, CancellationToken _) =>
            {
                var cfg = ValePedagioProviderConfigurationFactory.CreateDefault(t, p);
                cfg.EndpointBaseUrl = "https://stub.local";
                return cfg;
            });
        return mock.Object;
    }

    private static ValePedagioProviderOperationContext Ctx() => new(
        "t1", "trans", "moto", "vei",
        new[] { "c1" },
        new ValePedagioRoute("SP", "RJ", new[] { "MG" }, new[] { "P1" }),
        100m, "doc", null, "obs");

    private static ValePedagioSolicitacao Sol(ValePedagioProviderType p) =>
        new(Guid.NewGuid(), "t1", p, ValePedagioIntegrationMode.Real,
            ValePedagioFlowType.QuoteAndPurchase, "tr", "m", "v", new[] { "c1" },
            new ValePedagioRoute("SP", "RJ", Array.Empty<string>(), Array.Empty<string>()),
            100m, null, null, null);

    [Fact]
    public async Task Ambipar_QuoteAsync_RetornaResultado()
    {
        var handler = new StubHttpHandler(_ => Json(HttpStatusCode.OK,
            """{"protocolo":"P","numeroCompra":"N","valorTotal":50.0,"status":"quoted"}"""));
        var http = new HttpClient(handler);
        var client = new AmbiparHttpClient(http, NullLogger<AmbiparHttpClient>.Instance);
        var provider = new AmbiparValePedagioProvider(RepoFor(ValePedagioProviderType.Ambipar), client);
        var r = await provider.QuoteAsync(Ctx());
        Assert.Equal("N", r.NumeroCompra);
        Assert.Equal(ValePedagioStatus.Cotado, r.SuggestedStatus);
    }

    [Fact]
    public async Task Ambipar_Purchase_StatusComprado()
    {
        var handler = new StubHttpHandler(_ => Json(HttpStatusCode.OK,
            """{"protocolo":"P","numeroCompra":"N","valorTotal":99.5,"status":"purchased"}"""));
        var client = new AmbiparHttpClient(new HttpClient(handler), NullLogger<AmbiparHttpClient>.Instance);
        var provider = new AmbiparValePedagioProvider(RepoFor(ValePedagioProviderType.Ambipar), client);
        var r = await provider.PurchaseAsync(Ctx());
        Assert.Equal(ValePedagioStatus.Comprado, r.SuggestedStatus);
        Assert.Equal(99.5m, r.ValorTotal);
    }

    [Fact]
    public async Task Ambipar_Sync_DevolveStatusDoServidor()
    {
        var handler = new StubHttpHandler(_ => Json(HttpStatusCode.OK,
            """{"numeroCompra":"N","status":"confirmed","valorTotal":10}"""));
        var client = new AmbiparHttpClient(new HttpClient(handler), NullLogger<AmbiparHttpClient>.Instance);
        var provider = new AmbiparValePedagioProvider(RepoFor(ValePedagioProviderType.Ambipar), client);
        var sol = Sol(ValePedagioProviderType.Ambipar);
        sol.ApplyPurchase(new ValePedagioProviderOperationResult("P", "N", 10m, Array.Empty<ValePedagioRegulatoryItem>(), null, "{}", "purchased", ValePedagioStatus.Comprado), "{}");
        var r = await provider.SyncAsync(sol);
        Assert.Equal(ValePedagioStatus.Confirmado, r.SuggestedStatus);
    }

    [Fact]
    public async Task Ambipar_Cancel_RetornaCancelado()
    {
        var handler = new StubHttpHandler(_ => Json(HttpStatusCode.OK,
            """{"numeroCompra":"N","status":"cancelled"}"""));
        var client = new AmbiparHttpClient(new HttpClient(handler), NullLogger<AmbiparHttpClient>.Instance);
        var provider = new AmbiparValePedagioProvider(RepoFor(ValePedagioProviderType.Ambipar), client);
        var sol = Sol(ValePedagioProviderType.Ambipar);
        sol.ApplyPurchase(new ValePedagioProviderOperationResult("P", "N", 10m, Array.Empty<ValePedagioRegulatoryItem>(), null, "{}", "purchased", ValePedagioStatus.Comprado), "{}");
        var r = await provider.CancelAsync(sol);
        Assert.Equal(ValePedagioStatus.Cancelado, r.SuggestedStatus);
    }

    [Fact]
    public async Task Ambipar_GetReceipt_ComBase64()
    {
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("recibo"));
        var handler = new StubHttpHandler(_ => Json(HttpStatusCode.OK,
            $$"""{"reciboBase64":"{{b64}}","reciboFileName":"r.pdf","reciboContentType":"application/pdf"}"""));
        var client = new AmbiparHttpClient(new HttpClient(handler), NullLogger<AmbiparHttpClient>.Instance);
        var provider = new AmbiparValePedagioProvider(RepoFor(ValePedagioProviderType.Ambipar), client);
        var sol = Sol(ValePedagioProviderType.Ambipar);
        sol.ApplyPurchase(new ValePedagioProviderOperationResult("P", "N-OK", 10m, Array.Empty<ValePedagioRegulatoryItem>(), null, "{}", "purchased", ValePedagioStatus.Comprado), "{}");
        var receipt = await provider.GetReceiptAsync(sol);
        Assert.NotNull(receipt);
        Assert.Equal("r.pdf", receipt!.FileName);
    }

    [Fact]
    public async Task Ambipar_GetReceipt_SemNumeroCompra_RetornaNull()
    {
        var handler = new StubHttpHandler();
        var client = new AmbiparHttpClient(new HttpClient(handler), NullLogger<AmbiparHttpClient>.Instance);
        var provider = new AmbiparValePedagioProvider(RepoFor(ValePedagioProviderType.Ambipar), client);
        var sol = Sol(ValePedagioProviderType.Ambipar); // sem ApplyPurchase => sem NumeroCompra
        var receipt = await provider.GetReceiptAsync(sol);
        Assert.Null(receipt);
    }

    [Fact]
    public async Task Ambipar_HttpError_LancaInvalidOperation()
    {
        var handler = new StubHttpHandler(_ => Json(HttpStatusCode.BadRequest, """{"message":"erro"}"""));
        var client = new AmbiparHttpClient(new HttpClient(handler), NullLogger<AmbiparHttpClient>.Instance);
        var provider = new AmbiparValePedagioProvider(RepoFor(ValePedagioProviderType.Ambipar), client);
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.QuoteAsync(Ctx()));
    }

    [Fact]
    public async Task Extratta_Retry_EmFalha500_E_SucessoNaSegundaTentativa()
    {
        var handler = new StubHttpHandler(
            _ => Json(HttpStatusCode.InternalServerError, "{}"),
            _ => Json(HttpStatusCode.OK, """{"numeroCompra":"OK","valorTotal":1.0,"status":"quoted"}"""));
        var client = new ExtrattaHttpClient(new HttpClient(handler), NullLogger<ExtrattaHttpClient>.Instance);
        var provider = new ExtrattaValePedagioProvider(RepoFor(ValePedagioProviderType.Extratta), client);
        var r = await provider.QuoteAsync(Ctx());
        Assert.Equal("OK", r.NumeroCompra);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Pamcard_PurchaseCallbackMode_StatusEmProcessamento()
    {
        var handler = new StubHttpHandler(_ => Json(HttpStatusCode.OK,
            """{"numeroCompra":"N","valorTotal":10}"""));
        var client = new PamcardHttpClient(new HttpClient(handler), NullLogger<PamcardHttpClient>.Instance);
        var provider = new PamcardValePedagioProvider(RepoFor(ValePedagioProviderType.Pamcard), client);
        var r = await provider.PurchaseAsync(Ctx());
        Assert.Equal(ValePedagioStatus.EmProcessamento, r.SuggestedStatus);
    }
}
