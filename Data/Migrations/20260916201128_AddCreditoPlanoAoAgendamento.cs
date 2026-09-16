using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditoPlanoAoAgendamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssinaturaPlanoServicoId",
                table: "Agendamentos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Agendamentos_AssinaturaPlanoServicoId",
                table: "Agendamentos",
                column: "AssinaturaPlanoServicoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Agendamentos_AssinaturasPlanoServico_AssinaturaPlanoServicoId",
                table: "Agendamentos",
                column: "AssinaturaPlanoServicoId",
                principalTable: "AssinaturasPlanoServico",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Agendamentos_AssinaturasPlanoServico_AssinaturaPlanoServicoId",
                table: "Agendamentos");

            migrationBuilder.DropIndex(
                name: "IX_Agendamentos_AssinaturaPlanoServicoId",
                table: "Agendamentos");

            migrationBuilder.DropColumn(
                name: "AssinaturaPlanoServicoId",
                table: "Agendamentos");
        }
    }
}
