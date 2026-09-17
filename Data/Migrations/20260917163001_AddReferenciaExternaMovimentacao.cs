using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenciaExternaMovimentacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferenciaExterna",
                table: "MovimentacoesFinanceiras",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesFinanceiras_EmpresaId_ReferenciaExterna",
                table: "MovimentacoesFinanceiras",
                columns: new[] { "EmpresaId", "ReferenciaExterna" },
                unique: true,
                filter: "[ReferenciaExterna] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MovimentacoesFinanceiras_EmpresaId_ReferenciaExterna",
                table: "MovimentacoesFinanceiras");

            migrationBuilder.DropColumn(
                name: "ReferenciaExterna",
                table: "MovimentacoesFinanceiras");
        }
    }
}
