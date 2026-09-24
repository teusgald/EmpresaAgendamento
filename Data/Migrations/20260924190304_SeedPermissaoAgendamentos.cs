using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedPermissaoAgendamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =========================================================
            // SEED — "Agendamentos" não existia como módulo quando os
            // perfis foram semeados. Cria a linha pra todo Perfil já
            // existente com Escopo = Proprios (1), que é exatamente o
            // comportamento hoje hardcoded em AgendamentosController —
            // ninguém muda de comportamento até o dono da empresa entrar
            // em Perfis de acesso e trocar pra "Todos" manualmente.
            // Ver/Criar/Editar/Excluir ficam true só por completude — a
            // tela de Agendamentos continua sempre liberada, essas 4
            // flags não são lidas por nenhum filtro.
            // =========================================================
            migrationBuilder.Sql(@"
                INSERT INTO [PerfilPermissoes] ([PerfilId], [Modulo], [PodeVisualizar], [PodeCriar], [PodeEditar], [PodeExcluir], [EscopoDados])
                SELECT p.[Id], N'Agendamentos', 1, 1, 1, 1, 1
                FROM [Perfis] p
                WHERE NOT EXISTS (
                    SELECT 1 FROM [PerfilPermissoes] pp
                    WHERE pp.[PerfilId] = p.[Id] AND pp.[Modulo] = N'Agendamentos'
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
