using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class AjustarAssinaturaPlanoServico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PercentualJurosAtraso",
                table: "PlanosServico",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataAprovacao",
                table: "AssinaturasPlanoServico",
                type: "datetime2",
                nullable: true);

            // Default 1 (Pendente) só pra ter algo válido enquanto o backfill
            // abaixo não roda — toda linha existente é imediatamente
            // corrigida pra 2 (Ativa) ou 3 (Cancelada) a partir do Ativa antigo.
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "AssinaturasPlanoServico",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                "UPDATE AssinaturasPlanoServico SET Status = CASE WHEN Ativa = 1 THEN 2 ELSE 3 END;");

            migrationBuilder.DropColumn(
                name: "Ativa",
                table: "AssinaturasPlanoServico");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Ativa",
                table: "AssinaturasPlanoServico",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE AssinaturasPlanoServico SET Ativa = CASE WHEN Status = 2 THEN 1 ELSE 0 END;");

            migrationBuilder.DropColumn(
                name: "PercentualJurosAtraso",
                table: "PlanosServico");

            migrationBuilder.DropColumn(
                name: "DataAprovacao",
                table: "AssinaturasPlanoServico");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AssinaturasPlanoServico");
        }
    }
}
