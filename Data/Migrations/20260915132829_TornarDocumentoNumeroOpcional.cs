using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class TornarDocumentoNumeroOpcional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Empresas_DocumentoNumero",
                table: "Empresas");

            migrationBuilder.AlterColumn<string>(
                name: "DocumentoNumero",
                table: "Empresas",
                type: "nvarchar(18)",
                maxLength: 18,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(18)",
                oldMaxLength: 18);

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_DocumentoNumero",
                table: "Empresas",
                column: "DocumentoNumero",
                unique: true,
                filter: "[DocumentoNumero] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Empresas_DocumentoNumero",
                table: "Empresas");

            migrationBuilder.AlterColumn<string>(
                name: "DocumentoNumero",
                table: "Empresas",
                type: "nvarchar(18)",
                maxLength: 18,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(18)",
                oldMaxLength: 18,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_DocumentoNumero",
                table: "Empresas",
                column: "DocumentoNumero",
                unique: true);
        }
    }
}
