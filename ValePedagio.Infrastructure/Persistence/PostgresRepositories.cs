using Microsoft.EntityFrameworkCore;
using ValePedagio.Domain;

namespace ValePedagio.Infrastructure.Persistence;

public sealed class PostgresValePedagioSolicitacaoRepository : IValePedagioSolicitacaoRepository
{
    private readonly ValePedagioDbContext _dbContext;

    public PostgresValePedagioSolicitacaoRepository(ValePedagioDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddOrUpdateAsync(ValePedagioSolicitacao solicitacao, CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.Solicitacoes
            .AsNoTracking()
            .AnyAsync(item => item.Id == solicitacao.Id, cancellationToken);

        if (exists)
        {
            _dbContext.Solicitacoes.Update(solicitacao);
        }
        else
        {
            await _dbContext.Solicitacoes.AddAsync(solicitacao, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<ValePedagioSolicitacao?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Solicitacoes
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ValePedagioSolicitacao>> ListAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Solicitacoes
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}

public sealed class PostgresValePedagioProviderConfigurationRepository : IValePedagioProviderConfigurationRepository
{
    private readonly ValePedagioDbContext _dbContext;

    public PostgresValePedagioProviderConfigurationRepository(ValePedagioDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ValePedagioProviderConfiguration> GetAsync(string tenantId, ValePedagioProviderType provider, CancellationToken cancellationToken = default)
    {
        var config = await _dbContext.ProviderConfigurations
            .FirstOrDefaultAsync(
                item => item.TenantId == tenantId && item.Provider == provider,
                cancellationToken);

        if (config is not null)
        {
            return config;
        }

        config = ValePedagioProviderConfigurationFactory.CreateDefault(tenantId, provider);
        await _dbContext.ProviderConfigurations.AddAsync(config, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return config;
    }

    public async Task SaveAsync(ValePedagioProviderConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.ProviderConfigurations
            .AsNoTracking()
            .AnyAsync(
                item => item.TenantId == configuration.TenantId && item.Provider == configuration.Provider,
                cancellationToken);

        if (exists)
        {
            _dbContext.ProviderConfigurations.Update(configuration);
        }
        else
        {
            await _dbContext.ProviderConfigurations.AddAsync(configuration, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ValePedagioProviderConfiguration>> ListAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        foreach (var descriptor in ValePedagioProviderCatalog.Descriptors)
        {
            _ = await GetAsync(tenantId, descriptor.Type, cancellationToken);
        }

        return await _dbContext.ProviderConfigurations
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.Wave)
            .ThenBy(item => item.DisplayName)
            .ToListAsync(cancellationToken);
    }
}
