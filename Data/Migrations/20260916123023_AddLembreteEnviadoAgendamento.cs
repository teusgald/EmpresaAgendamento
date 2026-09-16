using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLembreteEnviadoAgendamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LembreteEnviado",
                table: "Agendamentos",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LembreteEnviado",
                table: "Agendamentos");
        }
    }
}
