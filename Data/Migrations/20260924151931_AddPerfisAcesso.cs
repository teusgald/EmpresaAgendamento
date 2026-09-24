using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerfisAcesso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PerfilId",
                table: "Funcionarios",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Perfis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Perfis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Perfis_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PerfilPermissoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PerfilId = table.Column<int>(type: "int", nullable: false),
                    Modulo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PodeVisualizar = table.Column<bool>(type: "bit", nullable: false),
                    PodeCriar = table.Column<bool>(type: "bit", nullable: false),
                    PodeEditar = table.Column<bool>(type: "bit", nullable: false),
                    PodeExcluir = table.Column<bool>(type: "bit", nullable: false),
                    EscopoDados = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerfilPermissoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerfilPermissoes_Perfis_PerfilId",
                        column: x => x.PerfilId,
                        principalTable: "Perfis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Funcionarios_PerfilId",
                table: "Funcionarios",
                column: "PerfilId");

            migrationBuilder.CreateIndex(
                name: "IX_PerfilPermissoes_PerfilId",
                table: "PerfilPermissoes",
                column: "PerfilId");

            migrationBuilder.CreateIndex(
                name: "IX_Perfis_EmpresaId",
                table: "Perfis",
                column: "EmpresaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Funcionarios_Perfis_PerfilId",
                table: "Funcionarios",
                column: "PerfilId",
                principalTable: "Perfis",
                principalColumn: "Id");

            // =========================================================
            // SEED — replica o comportamento de acesso de hoje em cima do
            // novo modelo, pra nenhuma empresa já existente perder acesso a
            // nada no dia do deploy (ver plano Fase 2 — "migração segura").
            //
            // Hoje, sem nenhum gate em ClientesController, QUALQUER
            // funcionário (Padrão ou Gerente) tem acesso total a Clientes —
            // por isso os dois perfis default ganham Clientes liberado.
            // Os outros 8 módulos só abrem pra quem já era Gerente, exatamente
            // como o RequerGerenteFilter já fazia.
            // =========================================================

            migrationBuilder.Sql(@"
                INSERT INTO [Perfis] ([EmpresaId], [Nome], [Ativo])
                SELECT [Id], N'Padrão', 1 FROM [Empresas];

                INSERT INTO [Perfis] ([EmpresaId], [Nome], [Ativo])
                SELECT [Id], N'Gerente', 1 FROM [Empresas];

                -- Padrão: só Clientes, igual ao acesso irrestrito de hoje.
                INSERT INTO [PerfilPermissoes] ([PerfilId], [Modulo], [PodeVisualizar], [PodeCriar], [PodeEditar], [PodeExcluir], [EscopoDados])
                SELECT [Id], N'Clientes', 1, 1, 1, 1, 2 FROM [Perfis] WHERE [Nome] = N'Padrão';

                -- Gerente: Clientes + os 8 módulos que hoje já são exclusivos de Gerente.
                INSERT INTO [PerfilPermissoes] ([PerfilId], [Modulo], [PodeVisualizar], [PodeCriar], [PodeEditar], [PodeExcluir], [EscopoDados])
                SELECT p.[Id], m.[Modulo], 1, 1, 1, 1, 2
                FROM [Perfis] p
                CROSS JOIN (VALUES
                    (N'Clientes'), (N'Servicos'), (N'Produtos'), (N'Financeiro'),
                    (N'Fidelidade'), (N'ContasReceber'), (N'ContasPagar'),
                    (N'Comissoes'), (N'CategoriasFinanceiras')
                ) AS m([Modulo])
                WHERE p.[Nome] = N'Gerente';

                -- Atribui o Perfil recém-criado a cada Funcionario já existente,
                -- conforme o NivelAcesso dele hoje. NivelAcesso é salvo como
                -- string (nome do enum em C#: 'Padrao'/'Gerente', sem
                -- acento) — não é int, por isso comparar contra o nome, não
                -- contra 1/2.
                UPDATE f
                SET f.[PerfilId] = p.[Id]
                FROM [Funcionarios] f
                INNER JOIN [Perfis] p
                    ON p.[EmpresaId] = f.[EmpresaId]
                    AND p.[Nome] = CASE WHEN f.[NivelAcesso] = N'Gerente' THEN N'Gerente' ELSE N'Padrão' END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Funcionarios_Perfis_PerfilId",
                table: "Funcionarios");

            migrationBuilder.DropTable(
                name: "PerfilPermissoes");

            migrationBuilder.DropTable(
                name: "Perfis");

            migrationBuilder.DropIndex(
                name: "IX_Funcionarios_PerfilId",
                table: "Funcionarios");

            migrationBuilder.DropColumn(
                name: "PerfilId",
                table: "Funcionarios");
        }
    }
}
