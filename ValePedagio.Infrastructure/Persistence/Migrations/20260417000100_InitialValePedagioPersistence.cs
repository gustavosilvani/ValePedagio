using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ValePedagio.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ValePedagioDbContext))]
[Migration("20260417000100_InitialValePedagioPersistence")]
public sealed class InitialValePedagioPersistence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "vale_pedagio_provider_configurations",
            columns: table => new
            {
                TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Wave = table.Column<int>(type: "integer", nullable: false),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                EndpointBaseUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                CallbackMode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Credentials = table.Column<string>(type: "jsonb", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_vale_pedagio_provider_configurations", x => new { x.TenantId, x.Provider });
            });

        migrationBuilder.CreateTable(
            name: "vale_pedagio_solicitacoes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                TransportadorId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                MotoristaId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                VeiculoId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                CteIds = table.Column<string>(type: "jsonb", nullable: false),
                Route = table.Column<string>(type: "jsonb", nullable: false),
                EstimatedCargoValue = table.Column<decimal>(type: "numeric", nullable: false),
                DocumentoResponsavelPagamento = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                Observacoes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                CallbackUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Protocolo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                NumeroCompra = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                ValorTotal = table.Column<decimal>(type: "numeric", nullable: true),
                FailureReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                RetryCount = table.Column<int>(type: "integer", nullable: false),
                RawRequestPayload = table.Column<string>(type: "text", nullable: true),
                RawResponsePayload = table.Column<string>(type: "text", nullable: true),
                Receipt = table.Column<string>(type: "jsonb", nullable: true),
                RegulatoryItems = table.Column<string>(type: "jsonb", nullable: false),
                AuditTrail = table.Column<string>(type: "jsonb", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_vale_pedagio_solicitacoes", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_vale_pedagio_provider_configurations_TenantId",
            table: "vale_pedagio_provider_configurations",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_vale_pedagio_solicitacoes_TenantId_CreatedAt",
            table: "vale_pedagio_solicitacoes",
            columns: new[] { "TenantId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_vale_pedagio_solicitacoes_TenantId_Provider_Status",
            table: "vale_pedagio_solicitacoes",
            columns: new[] { "TenantId", "Provider", "Status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "vale_pedagio_provider_configurations");
        migrationBuilder.DropTable(name: "vale_pedagio_solicitacoes");
    }
}
