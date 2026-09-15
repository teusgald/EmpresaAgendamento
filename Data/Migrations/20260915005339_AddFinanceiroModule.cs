using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceiroModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContaPagarId",
                table: "AgendamentosFuncionarios",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CategoriasFinanceiras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Padrao = table.Column<bool>(type: "bit", nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmpresaId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasFinanceiras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoriasFinanceiras_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContasPagar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ValorPrevisto = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ValorPago = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    FormaPagamento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DataVencimento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataPagamento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataCancelamento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    FuncionarioId = table.Column<int>(type: "int", nullable: true),
                    CategoriaId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContasPagar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContasPagar_CategoriasFinanceiras_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "CategoriasFinanceiras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ContasPagar_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContasPagar_Funcionarios_FuncionarioId",
                        column: x => x.FuncionarioId,
                        principalTable: "Funcionarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ContasReceber",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ValorPrevisto = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ValorRecebido = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    FormaPagamento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DataVencimento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataRecebimento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataCancelamento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    AgendamentoId = table.Column<int>(type: "int", nullable: true),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    CategoriaId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContasReceber", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContasReceber_Agendamentos_AgendamentoId",
                        column: x => x.AgendamentoId,
                        principalTable: "Agendamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContasReceber_CategoriasFinanceiras_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "CategoriasFinanceiras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ContasReceber_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContasReceber_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimentacoesFinanceiras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Origem = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    FormaPagamento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DataMovimento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MovimentacaoOrigemEstornoId = table.Column<int>(type: "int", nullable: true),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    ContaReceberId = table.Column<int>(type: "int", nullable: true),
                    ContaPagarId = table.Column<int>(type: "int", nullable: true),
                    CategoriaId = table.Column<int>(type: "int", nullable: true),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentacoesFinanceiras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimentacoesFinanceiras_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MovimentacoesFinanceiras_CategoriasFinanceiras_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "CategoriasFinanceiras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MovimentacoesFinanceiras_ContasPagar_ContaPagarId",
                        column: x => x.ContaPagarId,
                        principalTable: "ContasPagar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MovimentacoesFinanceiras_ContasReceber_ContaReceberId",
                        column: x => x.ContaReceberId,
                        principalTable: "ContasReceber",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MovimentacoesFinanceiras_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentacoesFinanceiras_MovimentacoesFinanceiras_MovimentacaoOrigemEstornoId",
                        column: x => x.MovimentacaoOrigemEstornoId,
                        principalTable: "MovimentacoesFinanceiras",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgendamentosFuncionarios_ContaPagarId",
                table: "AgendamentosFuncionarios",
                column: "ContaPagarId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasFinanceiras_EmpresaId",
                table: "CategoriasFinanceiras",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_CategoriaId",
                table: "ContasPagar",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_EmpresaId",
                table: "ContasPagar",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_EmpresaId_DataVencimento",
                table: "ContasPagar",
                columns: new[] { "EmpresaId", "DataVencimento" });

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_FuncionarioId",
                table: "ContasPagar",
                column: "FuncionarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ContasReceber_AgendamentoId",
                table: "ContasReceber",
                column: "AgendamentoId",
                unique: true,
                filter: "[AgendamentoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContasReceber_CategoriaId",
                table: "ContasReceber",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_ContasReceber_ClienteId",
                table: "ContasReceber",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ContasReceber_EmpresaId",
                table: "ContasReceber",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_ContasReceber_EmpresaId_DataVencimento",
                table: "ContasReceber",
                columns: new[] { "EmpresaId", "DataVencimento" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesFinanceiras_CategoriaId",
                table: "MovimentacoesFinanceiras",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesFinanceiras_ContaPagarId",
                table: "MovimentacoesFinanceiras",
                column: "ContaPagarId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesFinanceiras_ContaReceberId",
                table: "MovimentacoesFinanceiras",
                column: "ContaReceberId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesFinanceiras_EmpresaId",
                table: "MovimentacoesFinanceiras",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesFinanceiras_EmpresaId_DataMovimento",
                table: "MovimentacoesFinanceiras",
                columns: new[] { "EmpresaId", "DataMovimento" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesFinanceiras_MovimentacaoOrigemEstornoId",
                table: "MovimentacoesFinanceiras",
                column: "MovimentacaoOrigemEstornoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesFinanceiras_UsuarioId",
                table: "MovimentacoesFinanceiras",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_AgendamentosFuncionarios_ContasPagar_ContaPagarId",
                table: "AgendamentosFuncionarios",
                column: "ContaPagarId",
                principalTable: "ContasPagar",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgendamentosFuncionarios_ContasPagar_ContaPagarId",
                table: "AgendamentosFuncionarios");

            migrationBuilder.DropTable(
                name: "MovimentacoesFinanceiras");

            migrationBuilder.DropTable(
                name: "ContasPagar");

            migrationBuilder.DropTable(
                name: "ContasReceber");

            migrationBuilder.DropTable(
                name: "CategoriasFinanceiras");

            migrationBuilder.DropIndex(
                name: "IX_AgendamentosFuncionarios_ContaPagarId",
                table: "AgendamentosFuncionarios");

            migrationBuilder.DropColumn(
                name: "ContaPagarId",
                table: "AgendamentosFuncionarios");
        }
    }
}
