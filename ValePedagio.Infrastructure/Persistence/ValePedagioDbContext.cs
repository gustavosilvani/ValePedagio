using Microsoft.EntityFrameworkCore;
using ValePedagio.Domain;

namespace ValePedagio.Infrastructure.Persistence;

public sealed class ValePedagioDbContext : DbContext
{
    public ValePedagioDbContext(DbContextOptions<ValePedagioDbContext> options)
        : base(options)
    {
    }

    public DbSet<ValePedagioProviderConfiguration> ProviderConfigurations => Set<ValePedagioProviderConfiguration>();

    public DbSet<ValePedagioSolicitacao> Solicitacoes => Set<ValePedagioSolicitacao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var configuration = modelBuilder.Entity<ValePedagioProviderConfiguration>();
        configuration.ToTable("vale_pedagio_provider_configurations");
        configuration.HasKey(item => new { item.TenantId, item.Provider });
        configuration.Property(item => item.TenantId).HasMaxLength(128);
        configuration.Property(item => item.Provider).HasConversion<string>().HasMaxLength(32);
        configuration.Property(item => item.DisplayName).HasMaxLength(128);
        configuration.Property(item => item.Wave);
        configuration.Property(item => item.EndpointBaseUrl).HasMaxLength(512);
        configuration.Property(item => item.CallbackMode).HasMaxLength(64);
        configuration.Property(item => item.Credentials).HasJsonbConversion();
        configuration.Property(item => item.UpdatedAt).HasColumnType("timestamp with time zone");
        configuration.HasIndex(item => item.TenantId);

        var solicitacao = modelBuilder.Entity<ValePedagioSolicitacao>();
        solicitacao.ToTable("vale_pedagio_solicitacoes");
        solicitacao.HasKey(item => item.Id);
        solicitacao.Property(item => item.Id).ValueGeneratedNever();
        solicitacao.Property(item => item.TenantId).HasMaxLength(128);
        solicitacao.Property(item => item.Provider).HasConversion<string>().HasMaxLength(32);
        solicitacao.Property(item => item.TransportadorId).HasMaxLength(128);
        solicitacao.Property(item => item.MotoristaId).HasMaxLength(128);
        solicitacao.Property(item => item.VeiculoId).HasMaxLength(128);
        solicitacao.Property(item => item.DocumentoResponsavelPagamento).HasMaxLength(32);
        solicitacao.Property(item => item.Observacoes).HasMaxLength(4000);
        solicitacao.Property(item => item.CallbackUrl).HasMaxLength(1024);
        solicitacao.Property(item => item.FlowType).HasConversion<string>().HasMaxLength(32);
        solicitacao.Property(item => item.IntegrationMode).HasConversion<string>().HasMaxLength(32);
        solicitacao.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
        solicitacao.Property(item => item.ProviderStatus).HasMaxLength(128);
        solicitacao.Property(item => item.Protocolo).HasMaxLength(128);
        solicitacao.Property(item => item.NumeroCompra).HasMaxLength(128);
        solicitacao.Property(item => item.FailureCategory).HasConversion<string>().HasMaxLength(32);
        solicitacao.Property(item => item.FailureReason).HasMaxLength(4000);
        solicitacao.Property(item => item.LastSyncAt).HasColumnType("timestamp with time zone");
        solicitacao.Property(item => item.NextRetryAt).HasColumnType("timestamp with time zone");
        solicitacao.Property(item => item.ConcludedAt).HasColumnType("timestamp with time zone");
        solicitacao.Property(item => item.RawRequestPayload).HasColumnType("text");
        solicitacao.Property(item => item.RawResponsePayload).HasColumnType("text");
        solicitacao.Property(item => item.CreatedAt).HasColumnType("timestamp with time zone");
        solicitacao.Property(item => item.UpdatedAt).HasColumnType("timestamp with time zone");
        solicitacao.Property(item => item.CteIds).HasJsonbConversion();
        solicitacao.Property(item => item.Route).HasJsonbConversion();
        solicitacao.Property(item => item.Receipt).HasJsonbConversion();
        solicitacao.Property(item => item.RegulatoryItems).HasJsonbConversion();
        solicitacao.Property(item => item.AuditTrail).HasJsonbConversion();
        solicitacao.Property(item => item.SyncAttempts).HasJsonbConversion();
        solicitacao.Property(item => item.ProviderArtifacts).HasJsonbConversion();
        solicitacao.HasIndex(item => new { item.TenantId, item.CreatedAt });
        solicitacao.HasIndex(item => new { item.TenantId, item.Provider, item.Status });
        solicitacao.HasIndex(item => new { item.TenantId, item.Provider, item.NumeroCompra });
    }
}
