using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using ValePedagio.Api;
using ValePedagio.Application;
using ValePedagio.Domain;
using ValePedagio.Infrastructure;
using ValePedagio.Infrastructure.Persistence;
using ValePedagio.Infrastructure.Providers;

namespace ValePedagio.Tests;

public sealed class ValePedagioApplicationServiceTests
{
    [Fact]
    public async Task PurchaseAsync_ShouldPersistReceiptAndRegulatoryData()
    {
        var service = CreateService(nameof(PurchaseAsync_ShouldPersistReceiptAndRegulatoryData));

        var response = await service.PurchaseAsync("tenant-a", CreateRequest(ValePedagioProviderType.EFrete));

        Assert.Equal(ValePedagioStatus.Comprado, response.Status);
        Assert.Equal(ValePedagioProviderType.EFrete, response.Provider);
        Assert.True(response.ReceiptAvailable);
        Assert.NotEmpty(response.ValesPedagio);

        var receipt = await service.GetReceiptAsync("tenant-a", response.Id);
        Assert.NotNull(receipt);
        Assert.NotEmpty(receipt!.Content);
    }

    [Fact]
    public async Task GetProviderConfigurationAsync_ShouldMaskSensitiveCredentials()
    {
        var databaseName = nameof(GetProviderConfigurationAsync_ShouldMaskSensitiveCredentials);
        await using var dbContext = CreateDbContext(databaseName);
        var repository = new PostgresValePedagioProviderConfigurationRepository(dbContext);
        var config = await repository.GetAsync("tenant-mask", ValePedagioProviderType.EFrete);
        config.Credentials["password"] = "segredo-real";
        config.Credentials["integratorHash"] = "hash-real";
        config.Credentials["username"] = "usuario-visivel";
        await repository.SaveAsync(config);

        var service = CreateService(databaseName);
        var dto = await service.GetProviderConfigurationAsync("tenant-mask", ValePedagioProviderType.EFrete);

        Assert.Equal(ValePedagioCredentialMasking.MaskPlaceholder, dto.Credentials["password"]);
        Assert.Equal(ValePedagioCredentialMasking.MaskPlaceholder, dto.Credentials["integratorHash"]);
        Assert.Equal("usuario-visivel", dto.Credentials["username"]);
    }

    [Fact]
    public async Task UpdateProviderConfigurationAsync_ShouldMergeCredentialsAndPreserveUnmentionedKeys()
    {
        var databaseName = nameof(UpdateProviderConfigurationAsync_ShouldMergeCredentialsAndPreserveUnmentionedKeys);
        await using var dbContext = CreateDbContext(databaseName);
        var repository = new PostgresValePedagioProviderConfigurationRepository(dbContext);
        var config = await repository.GetAsync("tenant-merge", ValePedagioProviderType.EFrete);
        config.Credentials["username"] = "antigo";
        config.Credentials["password"] = "segredo";
        config.Credentials["quoteAction"] = "urn:quote-custom";
        await repository.SaveAsync(config);

        var service = CreateService(databaseName);
        await service.UpdateProviderConfigurationAsync(
            "tenant-merge",
            ValePedagioProviderType.EFrete,
            new ValePedagioProviderConfigurationRequest(
                Enabled: true,
                EndpointBaseUrl: null,
                CallbackMode: null,
                Credentials: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["username"] = "novo"
                }));

        await using var db2 = CreateDbContext(databaseName);
        var repository2 = new PostgresValePedagioProviderConfigurationRepository(db2);
        var reloaded = await repository2.GetAsync("tenant-merge", ValePedagioProviderType.EFrete);
        Assert.Equal("novo", reloaded.Credentials["username"]);
        Assert.Equal("segredo", reloaded.Credentials["password"]);
        Assert.Equal("urn:quote-custom", reloaded.Credentials["quoteAction"]);
    }

    [Fact]
    public async Task UpdateProviderConfigurationAsync_ShouldRemoveCredentialWhenEmptyStringSent()
    {
        var databaseName = nameof(UpdateProviderConfigurationAsync_ShouldRemoveCredentialWhenEmptyStringSent);
        await using var dbContext = CreateDbContext(databaseName);
        var repository = new PostgresValePedagioProviderConfigurationRepository(dbContext);
        var config = await repository.GetAsync("tenant-clear", ValePedagioProviderType.EFrete);
        config.Credentials["token"] = "abc";
        await repository.SaveAsync(config);

        var service = CreateService(databaseName);
        await service.UpdateProviderConfigurationAsync(
            "tenant-clear",
            ValePedagioProviderType.EFrete,
            new ValePedagioProviderConfigurationRequest(
                Enabled: true,
                EndpointBaseUrl: null,
                CallbackMode: null,
                Credentials: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["token"] = "" }));

        await using var db2 = CreateDbContext(databaseName);
        var repository2 = new PostgresValePedagioProviderConfigurationRepository(db2);
        var reloaded = await repository2.GetAsync("tenant-clear", ValePedagioProviderType.EFrete);
        Assert.False(reloaded.Credentials.ContainsKey("token"));
    }

    [Fact]
    public async Task UpdateProviderConfigurationAsync_ShouldIgnoreMaskedPlaceholderForSecrets()
    {
        var databaseName = nameof(UpdateProviderConfigurationAsync_ShouldIgnoreMaskedPlaceholderForSecrets);
        await using var dbContext = CreateDbContext(databaseName);
        var repository = new PostgresValePedagioProviderConfigurationRepository(dbContext);
        var config = await repository.GetAsync("tenant-ph", ValePedagioProviderType.EFrete);
        config.Credentials["password"] = "real-secret";
        await repository.SaveAsync(config);

        var service = CreateService(databaseName);
        await service.UpdateProviderConfigurationAsync(
            "tenant-ph",
            ValePedagioProviderType.EFrete,
            new ValePedagioProviderConfigurationRequest(
                Enabled: true,
                EndpointBaseUrl: null,
                CallbackMode: null,
                Credentials: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["password"] = ValePedagioCredentialMasking.MaskPlaceholder,
                    ["username"] = "novo-user"
                }));

        await using var db2 = CreateDbContext(databaseName);
        var repository2 = new PostgresValePedagioProviderConfigurationRepository(db2);
        var reloaded = await repository2.GetAsync("tenant-ph", ValePedagioProviderType.EFrete);
        Assert.Equal("real-secret", reloaded.Credentials["password"]);
        Assert.Equal("novo-user", reloaded.Credentials["username"]);
    }

    [Fact]
    public async Task Configurations_ShouldRemainScopedPerTenantAcrossContexts()
    {
        var databaseName = nameof(Configurations_ShouldRemainScopedPerTenantAcrossContexts);

        await using (var firstContext = CreateDbContext(databaseName))
        {
            var repository = new PostgresValePedagioProviderConfigurationRepository(firstContext);
            var config = await repository.GetAsync("tenant-a", ValePedagioProviderType.EFrete);
            config.Enabled = false;
            config.EndpointBaseUrl = "https://tenant-a.local/efrete";
            config.Credentials["username"] = "tenant-a-user";
            await repository.SaveAsync(config);
        }

        await using var secondContext = CreateDbContext(databaseName);
        var secondRepository = new PostgresValePedagioProviderConfigurationRepository(secondContext);
        var tenantA = await secondRepository.GetAsync("tenant-a", ValePedagioProviderType.EFrete);
        var tenantB = await secondRepository.GetAsync("tenant-b", ValePedagioProviderType.EFrete);

        Assert.False(tenantA.Enabled);
        Assert.Equal("https://tenant-a.local/efrete", tenantA.EndpointBaseUrl);
        Assert.Equal("tenant-a-user", tenantA.Credentials["username"]);
        Assert.True(tenantB.Enabled);
        Assert.NotEqual(tenantA.EndpointBaseUrl, tenantB.EndpointBaseUrl);
    }

    [Fact]
    public async Task EFreteProvider_ShouldAuthenticateAndRunQuotePurchaseCancelAndReceipt()
    {
        var databaseName = nameof(EFreteProvider_ShouldAuthenticateAndRunQuotePurchaseCancelAndReceipt);
        var handler = new QueueHttpMessageHandler(
            SoapOk("<Token>token-123</Token>"),
            SoapOk("<Protocolo>PROTO-COT</Protocolo><NumeroCompra>COT-123</NumeroCompra><ValorTotal>45.67</ValorTotal>"),
            SoapOk("<Protocolo>PROTO-CMP</Protocolo><NumeroCompra>COMP-123</NumeroCompra><ValorTotal>89.10</ValorTotal><Arquivo>" + Convert.ToBase64String(Encoding.UTF8.GetBytes("recibo")) + "</Arquivo><NomeArquivo>recibo.txt</NomeArquivo><MimeType>text/plain</MimeType>"),
            SoapOk("<Protocolo>PROTO-REC</Protocolo><NumeroCompra>COMP-123</NumeroCompra><Arquivo>" + Convert.ToBase64String(Encoding.UTF8.GetBytes("recibo-atualizado")) + "</Arquivo><NomeArquivo>recibo-atualizado.txt</NomeArquivo><MimeType>text/plain</MimeType>"),
            SoapOk("<Protocolo>PROTO-CAN</Protocolo><NumeroCompra>COMP-123</NumeroCompra><ValorTotal>89.10</ValorTotal>"));

        await using var dbContext = CreateDbContext(databaseName);
        var configRepository = new PostgresValePedagioProviderConfigurationRepository(dbContext);
        var config = await configRepository.GetAsync("tenant-a", ValePedagioProviderType.EFrete);
        config.EndpointBaseUrl = "https://sandbox.efrete.local/services";
        config.Credentials["integratorHash"] = "hash-123";
        config.Credentials["username"] = "integrador";
        config.Credentials["password"] = "segredo";
        config.Credentials["quoteAction"] = "urn:quote";
        config.Credentials["purchaseAction"] = "urn:purchase";
        config.Credentials["receiptAction"] = "urn:receipt";
        config.Credentials["cancelAction"] = "urn:cancel";
        config.Credentials["logonAction"] = "urn:logon";
        await configRepository.SaveAsync(config);

        var httpClient = new HttpClient(handler);
        var soapClient = new EFreteSoapClient(httpClient, new MemoryCache(new MemoryCacheOptions()), NullLogger<EFreteSoapClient>.Instance);
        var provider = new EFreteValePedagioProvider(configRepository, soapClient);

        var quote = await provider.QuoteAsync(CreateContext("tenant-a"));
        var purchase = await provider.PurchaseAsync(CreateContext("tenant-a"));

        var solicitacao = new ValePedagioSolicitacao(
            Guid.NewGuid(),
            "tenant-a",
            ValePedagioProviderType.EFrete,
            "transportador-1",
            "motorista-1",
            "veiculo-1",
            ["cte-1"],
            new ValePedagioRoute("SP", "RJ", ["MG"], ["P1"]),
            150000m,
            "12345678901",
            "obs",
            "https://callback.local");
        solicitacao.ApplyPurchase(purchase, "{}");

        var receipt = await provider.GetReceiptAsync(solicitacao);
        var cancel = await provider.CancelAsync(solicitacao);

        Assert.Equal("PROTO-COT", quote.Protocolo);
        Assert.Equal("COT-123", quote.NumeroCompra);
        Assert.Equal(45.67m, quote.ValorTotal);
        Assert.Equal("COMP-123", purchase.NumeroCompra);
        Assert.NotNull(receipt);
        Assert.Equal("recibo-atualizado.txt", receipt!.FileName);
        Assert.Equal("PROTO-CAN", cancel.Protocolo);
        Assert.Contains(handler.Requests, request => request.Headers.TryGetValues("SOAPAction", out var values) && values.Contains("urn:logon"));
        Assert.Contains(handler.Requests, request => request.Headers.TryGetValues("SOAPAction", out var values) && values.Contains("urn:purchase"));
    }

    [Fact]
    public async Task EFreteProvider_ShouldThrowWhenSoapReturnsFailure()
    {
        var handler = new QueueHttpMessageHandler(
            SoapOk("<Token>token-123</Token>"),
            SoapEnvelope("<Sucesso>false</Sucesso><Mensagem>Operacao rejeitada</Mensagem>"));

        var settings = new EFreteProviderSettings(
            "https://sandbox.efrete.local/services",
            "hash",
            "user",
            "pass",
            null,
            "11444777000101",
            "TAG",
            "urn:logon-ns",
            "Login",
            "urn:logon",
            "LogonService.asmx",
            "1",
            "urn:vp-ns",
            null,
            new EFreteOperationConfiguration("ValePedagioService.asmx", "Quote", "urn:quote", "1", null),
            new EFreteOperationConfiguration("ValePedagioService.asmx", "Purchase", "urn:purchase", "1", null),
            new EFreteOperationConfiguration("ValePedagioService.asmx", "Cancel", "urn:cancel", "1", null),
            new EFreteOperationConfiguration("ValePedagioService.asmx", "Receipt", "urn:receipt", "1", null),
            TimeSpan.FromSeconds(5));

        var client = new EFreteSoapClient(new HttpClient(handler), new MemoryCache(new MemoryCacheOptions()), NullLogger<EFreteSoapClient>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.QuoteAsync(settings, CreateContext("tenant-a"), CancellationToken.None));

        Assert.Contains("Operacao rejeitada", exception.Message);
    }

    [Fact]
    public async Task Api_ShouldExecuteQuotePurchaseReceiptAndCancelFlow()
    {
        await using var factory = new ValePedagioApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-http");

        var request = CreateRequest();

        var quoteResponse = await client.PostAsJsonAsync("/api/v1/vale-pedagio/solicitacoes/cotar", request);
        var quoteBody = await quoteResponse.Content.ReadAsStringAsync();
        Assert.True(quoteResponse.IsSuccessStatusCode, $"Quote falhou com {(int)quoteResponse.StatusCode}: {quoteBody}");
        var quoted = await quoteResponse.Content.ReadFromJsonAsync<ValePedagioSolicitacaoResponse>();

        var purchaseResponse = await client.PostAsJsonAsync("/api/v1/vale-pedagio/solicitacoes/comprar", request);
        var purchaseBody = await purchaseResponse.Content.ReadAsStringAsync();
        Assert.True(purchaseResponse.IsSuccessStatusCode, $"Purchase falhou com {(int)purchaseResponse.StatusCode}: {purchaseBody}");
        var purchased = await purchaseResponse.Content.ReadFromJsonAsync<ValePedagioSolicitacaoResponse>();

        var getResponse = await client.GetAsync($"/api/v1/vale-pedagio/solicitacoes/{purchased!.Id}");
        var receiptResponse = await client.GetAsync($"/api/v1/vale-pedagio/solicitacoes/{purchased.Id}/recibo");
        var cancelResponse = await client.PostAsJsonAsync($"/api/v1/vale-pedagio/solicitacoes/{purchased.Id}/cancelar", new { });

        Assert.NotNull(quoted);
        Assert.NotNull(purchased);
        Assert.True(quoted!.Id != Guid.Empty);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal("text/plain", receiptResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
    }

    private static IValePedagioApplicationService CreateService(string databaseName)
    {
        var dbContext = CreateDbContext(databaseName);
        var configurationRepository = new PostgresValePedagioProviderConfigurationRepository(dbContext);
        var solicitacaoRepository = new PostgresValePedagioSolicitacaoRepository(dbContext);
        var providers = ValePedagioProviderCatalog.Descriptors
            .Select(static descriptor => (IValePedagioProvider)new CatalogValePedagioProvider(descriptor))
            .ToList();
        var resolver = new ValePedagioProviderResolver(providers, configurationRepository);
        return new ValePedagioApplicationService(resolver, solicitacaoRepository, configurationRepository);
    }

    private static ValePedagioDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ValePedagioDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ValePedagioDbContext(options);
    }

    private static ValePedagioSolicitacaoRequest CreateRequest(ValePedagioProviderType? provider = null)
    {
        return new ValePedagioSolicitacaoRequest(
            "transportador-1",
            "motorista-1",
            "veiculo-1",
            ["cte-1", "cte-2"],
            new ValePedagioRouteDto("SP", "RJ", ["MG"], ["Extrema", "Volta Redonda"]),
            125000m,
            provider,
            "12345678901",
            "Teste automatizado",
            "https://callback.local/vale-pedagio");
    }

    private static ValePedagioProviderOperationContext CreateContext(string tenantId)
    {
        return new ValePedagioProviderOperationContext(
            tenantId,
            "transportador-1",
            "motorista-1",
            "veiculo-1",
            ["cte-1"],
            new ValePedagioRoute("SP", "RJ", ["MG"], ["P1"]),
            150000m,
            "12345678901",
            "https://callback.local",
            "obs");
    }

    private static HttpResponseMessage SoapOk(string body)
    {
        return SoapEnvelope("<Sucesso>true</Sucesso>" + body);
    }

    private static HttpResponseMessage SoapEnvelope(string body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""
                <?xml version="1.0" encoding="utf-8"?>
                <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                  <soap:Body>
                    <Response>
                      {{body}}
                    </Response>
                  </soap:Body>
                </soap:Envelope>
                """,
                Encoding.UTF8,
                "text/xml")
        };
    }

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public QueueHttpMessageHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(CloneRequest(request));
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No queued response available.");
            }

            return Task.FromResult(_responses.Dequeue());
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);
            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (request.Content is not null)
            {
                var content = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                clone.Content = new StringContent(content, Encoding.UTF8, request.Content.Headers.ContentType?.MediaType ?? "text/plain");
            }

            return clone;
        }
    }

    private sealed class ValePedagioApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
    {
        private readonly string _databaseName = Guid.NewGuid().ToString("N");

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var efInMemoryProvider = new ServiceCollection()
                    .AddEntityFrameworkInMemoryDatabase()
                    .BuildServiceProvider();

                services.RemoveAll<ValePedagioDbContext>();
                services.RemoveAll<DbContextOptions<ValePedagioDbContext>>();
                services.RemoveAll<IValePedagioProvider>();
                services.RemoveAll<IValePedagioSolicitacaoRepository>();
                services.RemoveAll<IValePedagioProviderConfigurationRepository>();
                services.RemoveAll<IValePedagioProviderResolver>();
                services.RemoveAll<IValePedagioApplicationService>();

                services.AddDbContext<ValePedagioDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_databaseName);
                    options.UseInternalServiceProvider(efInMemoryProvider);
                });
                services.AddScoped<IValePedagioSolicitacaoRepository, PostgresValePedagioSolicitacaoRepository>();
                services.AddScoped<IValePedagioProviderConfigurationRepository, PostgresValePedagioProviderConfigurationRepository>();
                services.AddScoped<IValePedagioProviderResolver, ValePedagioProviderResolver>();
                services.AddScoped<IValePedagioApplicationService, ValePedagioApplicationService>();

                foreach (var descriptor in ValePedagioProviderCatalog.Descriptors)
                {
                    services.AddScoped<IValePedagioProvider>(_ => new CatalogValePedagioProvider(descriptor));
                }
            });
        }

        public new ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
