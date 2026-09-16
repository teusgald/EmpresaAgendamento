using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class AnistiarEmpresasExistentesPaywall : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A partir de agora, empresa sem assinatura ativa é bloqueada no
            // portal (RequerAssinaturaAtivaFilter). Isso só vale pra quem se
            // cadastrar dali pra frente — quem já usava o sistema antes dessa
            // trava existir não deve ser cobrado retroativamente nem travado
            // sem aviso. Marca todas as empresas já existentes como "active".
            migrationBuilder.Sql(
                "UPDATE Empresas SET AssinaturaStatus = 'active' WHERE AssinaturaStatus IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Não reversível com segurança — não sabemos quais já eram
            // "active" de verdade (via Stripe) antes desta migration.
        }
    }
}
