using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFiltroServicoEMultiplosProgramasFidelidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FidelidadeClientes_Empresas_EmpresaId",
                table: "FidelidadeClientes");

            migrationBuilder.DropIndex(
                name: "IX_ProgramasFidelidade_EmpresaId",
                table: "ProgramasFidelidade");

            migrationBuilder.RenameColumn(
                name: "EmpresaId",
                table: "FidelidadeClientes",
                newName: "ProgramaFidelidadeId");

            migrationBuilder.RenameIndex(
                name: "IX_FidelidadeClientes_EmpresaId_ClienteId",
                table: "FidelidadeClientes",
                newName: "IX_FidelidadeClientes_ProgramaFidelidadeId_ClienteId");

            migrationBuilder.AddColumn<int>(
                name: "ServicoId",
                table: "ProgramasFidelidade",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramasFidelidade_EmpresaId_ServicoId",
                table: "ProgramasFidelidade",
                columns: new[] { "EmpresaId", "ServicoId" },
                unique: true,
                filter: "[ServicoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramasFidelidade_ServicoId",
                table: "ProgramasFidelidade",
                column: "ServicoId");

            migrationBuilder.AddForeignKey(
                name: "FK_FidelidadeClientes_ProgramasFidelidade_ProgramaFidelidadeId",
                table: "FidelidadeClientes",
                column: "ProgramaFidelidadeId",
                principalTable: "ProgramasFidelidade",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProgramasFidelidade_Servicos_ServicoId",
                table: "ProgramasFidelidade",
                column: "ServicoId",
                principalTable: "Servicos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FidelidadeClientes_ProgramasFidelidade_ProgramaFidelidadeId",
                table: "FidelidadeClientes");

            migrationBuilder.DropForeignKey(
                name: "FK_ProgramasFidelidade_Servicos_ServicoId",
                table: "ProgramasFidelidade");

            migrationBuilder.DropIndex(
                name: "IX_ProgramasFidelidade_EmpresaId_ServicoId",
                table: "ProgramasFidelidade");

            migrationBuilder.DropIndex(
                name: "IX_ProgramasFidelidade_ServicoId",
                table: "ProgramasFidelidade");

            migrationBuilder.DropColumn(
                name: "ServicoId",
                table: "ProgramasFidelidade");

            migrationBuilder.RenameColumn(
                name: "ProgramaFidelidadeId",
                table: "FidelidadeClientes",
                newName: "EmpresaId");

            migrationBuilder.RenameIndex(
                name: "IX_FidelidadeClientes_ProgramaFidelidadeId_ClienteId",
                table: "FidelidadeClientes",
                newName: "IX_FidelidadeClientes_EmpresaId_ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramasFidelidade_EmpresaId",
                table: "ProgramasFidelidade",
                column: "EmpresaId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FidelidadeClientes_Empresas_EmpresaId",
                table: "FidelidadeClientes",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
