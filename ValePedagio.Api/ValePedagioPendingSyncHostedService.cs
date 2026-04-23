using ValePedagio.Application;

namespace ValePedagio.Api;

public sealed class ValePedagioPendingSyncHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ValePedagioPendingSyncHostedService> _logger;
    private readonly IConfiguration _configuration;

    public ValePedagioPendingSyncHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ValePedagioPendingSyncHostedService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue("ValePedagio:SyncWorker:Enabled", true);
        if (!enabled)
        {
            _logger.LogInformation("ValePedagio pending sync worker desabilitado por configuração.");
            return;
        }

        var intervalSeconds = Math.Clamp(_configuration.GetValue("ValePedagio:SyncWorker:IntervalSeconds", 300), 30, 3600);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao sincronizar solicitações pendentes de vale-pedágio.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IValePedagioApplicationService>();
        var processed = await service.SyncPendingAsync(cancellationToken);
        if (processed > 0)
        {
            _logger.LogInformation("ValePedagio pending sync processou {Count} solicitações.", processed);
        }
    }
}
