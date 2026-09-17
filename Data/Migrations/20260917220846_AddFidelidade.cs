using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFidelidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "VisitaFidelidadeContabilizada",
                table: "Agendamentos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "FidelidadeClientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    VisitasContadas = table.Column<int>(type: "int", nullable: false),
                    DescontosDisponiveis = table.Column<int>(type: "int", nullable: false),
                    DescontosUsados = table.Column<int>(type: "int", nullable: false),
                    DataUltimaAtualizacao = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FidelidadeClientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FidelidadeClientes_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FidelidadeClientes_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProgramasFidelidade",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    VisitasNecessarias = table.Column<int>(type: "int", nullable: false),
                    TipoDesconto = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ValorDesconto = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramasFidelidade", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramasFidelidade_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FidelidadeClientes_ClienteId",
                table: "FidelidadeClientes",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_FidelidadeClientes_EmpresaId_ClienteId",
                table: "FidelidadeClientes",
                columns: new[] { "EmpresaId", "ClienteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramasFidelidade_EmpresaId",
                table: "ProgramasFidelidade",
                column: "EmpresaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FidelidadeClientes");

            migrationBuilder.DropTable(
                name: "ProgramasFidelidade");

            migrationBuilder.DropColumn(
                name: "VisitaFidelidadeContabilizada",
                table: "Agendamentos");
        }
    }
}
