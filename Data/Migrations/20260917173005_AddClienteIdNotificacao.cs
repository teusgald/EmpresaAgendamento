using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClienteIdNotificacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClienteId",
                table: "Notificacoes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notificacoes_ClienteId_Lida_DataCriacao",
                table: "Notificacoes",
                columns: new[] { "ClienteId", "Lida", "DataCriacao" });

            migrationBuilder.AddForeignKey(
                name: "FK_Notificacoes_Clientes_ClienteId",
                table: "Notificacoes",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notificacoes_Clientes_ClienteId",
                table: "Notificacoes");

            migrationBuilder.DropIndex(
                name: "IX_Notificacoes_ClienteId_Lida_DataCriacao",
                table: "Notificacoes");

            migrationBuilder.DropColumn(
                name: "ClienteId",
                table: "Notificacoes");
        }
    }
}
