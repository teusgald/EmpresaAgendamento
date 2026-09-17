using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNivelAcessoFuncionario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StripePriceIdSemestral",
                table: "Planos",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorSemestral",
                table: "Planos",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NivelAcesso",
                table: "Funcionarios",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Padrao");

            migrationBuilder.AddColumn<string>(
                name: "NomePlanoEscolhido",
                table: "Empresas",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StripePriceIdSemestral",
                table: "Planos");

            migrationBuilder.DropColumn(
                name: "ValorSemestral",
                table: "Planos");

            migrationBuilder.DropColumn(
                name: "NivelAcesso",
                table: "Funcionarios");

            migrationBuilder.DropColumn(
                name: "NomePlanoEscolhido",
                table: "Empresas");
        }
    }
}
