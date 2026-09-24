using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidarPerfisAcesso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =========================================================
            // SEED — o módulo "Auditoria" não existia quando os perfis
            // "Padrão"/"Gerente" foram semeados (migration AddPerfisAcesso).
            // Replica aqui o que o RequerGerenteFilter já fazia com
            // Auditoria: só Gerente tinha acesso, Padrão nunca.
            // =========================================================
            migrationBuilder.Sql(@"
                INSERT INTO [PerfilPermissoes] ([PerfilId], [Modulo], [PodeVisualizar], [PodeCriar], [PodeEditar], [PodeExcluir], [EscopoDados])
                SELECT p.[Id], N'Auditoria', 1, 1, 1, 1, 2
                FROM [Perfis] p
                WHERE p.[Nome] = N'Gerente'
                  AND NOT EXISTS (
                      SELECT 1 FROM [PerfilPermissoes] pp
                      WHERE pp.[PerfilId] = p.[Id] AND pp.[Modulo] = N'Auditoria'
                  );
            ");

            migrationBuilder.DropColumn(
                name: "NivelAcesso",
                table: "Funcionarios");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NivelAcesso",
                table: "Funcionarios",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }
    }
}
