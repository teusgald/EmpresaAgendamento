using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConfirmarEmailUsuariosExistentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A partir de agora o login exige e-mail confirmado
            // (RequireConfirmedEmail = true). Contas criadas antes dessa
            // mudança nunca passaram por confirmação — sem isso, ficariam
            // subitamente travadas para sempre. Marca todas como confirmadas
            // (só as futuras, cadastradas a partir de agora, precisam confirmar).
            migrationBuilder.Sql("UPDATE AspNetUsers SET EmailConfirmed = 1 WHERE EmailConfirmed = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Não reversível com segurança — não sabemos quais já estavam
            // confirmados antes desta migration.
        }
    }
}
