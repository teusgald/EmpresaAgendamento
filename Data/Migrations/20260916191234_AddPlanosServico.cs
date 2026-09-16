using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanosServico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanosServico",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Periodicidade = table.Column<int>(type: "int", nullable: false),
                    QuantidadeUsos = table.Column<int>(type: "int", nullable: false),
                    ValorReferencia = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanosServico", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanosServico_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssinaturasPlanoServico",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanoServicoId = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    DataInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataInicioPeriodoAtual = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreditosUsados = table.Column<int>(type: "int", nullable: false),
                    Ativa = table.Column<bool>(type: "bit", nullable: false),
                    DataCancelamento = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssinaturasPlanoServico", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssinaturasPlanoServico_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssinaturasPlanoServico_PlanosServico_PlanoServicoId",
                        column: x => x.PlanoServicoId,
                        principalTable: "PlanosServico",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanosServicoItens",
                columns: table => new
                {
                    PlanoServicoId = table.Column<int>(type: "int", nullable: false),
                    ServicoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanosServicoItens", x => new { x.PlanoServicoId, x.ServicoId });
                    table.ForeignKey(
                        name: "FK_PlanosServicoItens_PlanosServico_PlanoServicoId",
                        column: x => x.PlanoServicoId,
                        principalTable: "PlanosServico",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanosServicoItens_Servicos_ServicoId",
                        column: x => x.ServicoId,
                        principalTable: "Servicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssinaturasPlanoServico_ClienteId",
                table: "AssinaturasPlanoServico",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_AssinaturasPlanoServico_PlanoServicoId",
                table: "AssinaturasPlanoServico",
                column: "PlanoServicoId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanosServico_EmpresaId",
                table: "PlanosServico",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanosServicoItens_ServicoId",
                table: "PlanosServicoItens",
                column: "ServicoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssinaturasPlanoServico");

            migrationBuilder.DropTable(
                name: "PlanosServicoItens");

            migrationBuilder.DropTable(
                name: "PlanosServico");
        }
    }
}
