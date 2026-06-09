using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class ClienteOpcionalAgendamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ClienteAvulso",
                table: "Agendamentos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FuncionarioId",
                table: "Agendamentos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NomeClienteAvulso",
                table: "Agendamentos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelefoneClienteAvulso",
                table: "Agendamentos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Agendamentos_FuncionarioId",
                table: "Agendamentos",
                column: "FuncionarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Agendamentos_Funcionarios_FuncionarioId",
                table: "Agendamentos",
                column: "FuncionarioId",
                principalTable: "Funcionarios",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Agendamentos_Funcionarios_FuncionarioId",
                table: "Agendamentos");

            migrationBuilder.DropIndex(
                name: "IX_Agendamentos_FuncionarioId",
                table: "Agendamentos");

            migrationBuilder.DropColumn(
                name: "ClienteAvulso",
                table: "Agendamentos");

            migrationBuilder.DropColumn(
                name: "FuncionarioId",
                table: "Agendamentos");

            migrationBuilder.DropColumn(
                name: "NomeClienteAvulso",
                table: "Agendamentos");

            migrationBuilder.DropColumn(
                name: "TelefoneClienteAvulso",
                table: "Agendamentos");
        }
    }
}
