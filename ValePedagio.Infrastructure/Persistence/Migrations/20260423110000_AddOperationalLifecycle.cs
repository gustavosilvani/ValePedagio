using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ValePedagio.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ValePedagioDbContext))]
[Migration("20260423110000_AddOperationalLifecycle")]
public sealed class AddOperationalLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "FailureCategory",
            table: "vale_pedagio_solicitacoes",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "None");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ConcludedAt",
            table: "vale_pedagio_solicitacoes",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "FlowType",
            table: "vale_pedagio_solicitacoes",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "QuoteOnly");

        migrationBuilder.AddColumn<string>(
            name: "IntegrationMode",
            table: "vale_pedagio_solicitacoes",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "Simulated");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastSyncAt",
            table: "vale_pedagio_solicitacoes",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "NextRetryAt",
            table: "vale_pedagio_solicitacoes",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProviderArtifacts",
            table: "vale_pedagio_solicitacoes",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "ProviderStatus",
            table: "vale_pedagio_solicitacoes",
            type: "character varying(128)",
            maxLength: 128,
            nullable: false,
            defaultValue: "migrated");

        migrationBuilder.AddColumn<string>(
            name: "SyncAttempts",
            table: "vale_pedagio_solicitacoes",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.CreateIndex(
            name: "IX_vale_pedagio_solicitacoes_TenantId_Provider_NumeroCompra",
            table: "vale_pedagio_solicitacoes",
            columns: new[] { "TenantId", "Provider", "NumeroCompra" });

        migrationBuilder.Sql("""
            UPDATE vale_pedagio_solicitacoes
            SET "IntegrationMode" = CASE WHEN "Provider" = 'EFrete' THEN 'Real' ELSE 'Simulated' END,
                "FlowType" = CASE WHEN "Status" IN ('Comprado', 'Confirmado', 'Cancelado', 'Encerrado') THEN 'QuoteAndPurchase' ELSE 'QuoteOnly' END,
                "ProviderStatus" = CASE
                    WHEN "Status" = 'Cotado' THEN 'quoted'
                    WHEN "Status" = 'Comprado' THEN 'purchased'
                    WHEN "Status" = 'Confirmado' THEN 'confirmed'
                    WHEN "Status" = 'Cancelado' THEN 'cancelled'
                    WHEN "Status" = 'Falha' THEN 'failed'
                    ELSE 'processing'
                END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_vale_pedagio_solicitacoes_TenantId_Provider_NumeroCompra",
            table: "vale_pedagio_solicitacoes");

        migrationBuilder.DropColumn(name: "FailureCategory", table: "vale_pedagio_solicitacoes");
        migrationBuilder.DropColumn(name: "ConcludedAt", table: "vale_pedagio_solicitacoes");
        migrationBuilder.DropColumn(name: "FlowType", table: "vale_pedagio_solicitacoes");
        migrationBuilder.DropColumn(name: "IntegrationMode", table: "vale_pedagio_solicitacoes");
        migrationBuilder.DropColumn(name: "LastSyncAt", table: "vale_pedagio_solicitacoes");
        migrationBuilder.DropColumn(name: "NextRetryAt", table: "vale_pedagio_solicitacoes");
        migrationBuilder.DropColumn(name: "ProviderArtifacts", table: "vale_pedagio_solicitacoes");
        migrationBuilder.DropColumn(name: "ProviderStatus", table: "vale_pedagio_solicitacoes");
        migrationBuilder.DropColumn(name: "SyncAttempts", table: "vale_pedagio_solicitacoes");
    }
}
